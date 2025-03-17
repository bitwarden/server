using System.Text;
using System.Text.Json;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Api.Request.Opaque;
using Bit.Core.Auth.Models.Api.Response.Opaque;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bitwarden.OPAQUE;
using Microsoft.Extensions.Caching.Distributed;

namespace Bit.Core.Auth.Services;

#nullable enable

public class OpaqueKeyExchangeService : IOpaqueKeyExchangeService
{
    private readonly BitwardenOpaqueServer _bitwardenOpaque;
    private readonly IOpaqueKeyExchangeCredentialRepository _opaqueKeyExchangeCredentialRepository;
    private readonly IDistributedCache _distributedCache;
    private readonly IUserRepository _userRepository;

    public OpaqueKeyExchangeService(
        IOpaqueKeyExchangeCredentialRepository opaqueKeyExchangeCredentialRepository,
        IDistributedCache distributedCache,
        IUserRepository userRepository
        )
    {
        _bitwardenOpaque = new BitwardenOpaqueServer();
        _opaqueKeyExchangeCredentialRepository = opaqueKeyExchangeCredentialRepository;
        _distributedCache = distributedCache;
        _userRepository = userRepository;
    }

    public async Task<OpaqueRegistrationStartResponse> StartRegistration(byte[] request, User user, Models.Api.Request.Opaque.CipherConfiguration cipherConfiguration)
    {
        return await Task.Run(() =>
        {
            var registrationRequest = _bitwardenOpaque.StartRegistration(cipherConfiguration.ToNativeConfiguration(), null, request, user.Id.ToString());
            var registrationReseponse = registrationRequest.registrationResponse;
            var serverSetup = registrationRequest.serverSetup;
            // persist server setup
            var sessionId = Guid.NewGuid();
            SessionStore.RegisterSessions.Add(sessionId, new RegisterSession() { SessionId = sessionId, ServerSetup = serverSetup, CipherConfiguration = cipherConfiguration, UserId = user.Id });
            return new OpaqueRegistrationStartResponse(sessionId, Convert.ToBase64String(registrationReseponse));
        });
    }

    public async Task FinishRegistration(Guid sessionId, byte[] registrationUpload, User user, RotateableOpaqueKeyset keyset)
    {
        await Task.Run(() =>
        {
            var cipherConfiguration = SessionStore.RegisterSessions[sessionId].CipherConfiguration;
            try
            {
                var registrationFinish = _bitwardenOpaque.FinishRegistration(cipherConfiguration.ToNativeConfiguration(), registrationUpload);
                SessionStore.RegisterSessions[sessionId].PasswordFile = registrationFinish.serverRegistration;

                SessionStore.RegisterSessions[sessionId].KeySet = Encoding.ASCII.GetBytes(JsonSerializer.Serialize(keyset));
            }
            catch (Exception e)
            {
                SessionStore.RegisterSessions.Remove(sessionId);
                throw new Exception(e.Message);
            }
        });
    }

    public async Task<(Guid, byte[])> StartLogin(byte[] request, string email)
    {
        var user = await _userRepository.GetByEmailAsync(email);
        if (user == null)
        {
            // todo don't allow user enumeration
            throw new InvalidOperationException("User not found");
        }

        var credential = await _opaqueKeyExchangeCredentialRepository.GetByUserIdAsync(user.Id);
        if (credential == null)
        {
            // generate fake credential
            throw new InvalidOperationException("Credential not found");
        }

        var cipherConfiguration = JsonSerializer.Deserialize<Models.Api.Request.Opaque.CipherConfiguration>(credential.CipherConfiguration)!;
        var credentialBlob = JsonSerializer.Deserialize<OpaqueKeyExchangeCredentialBlob>(credential.CredentialBlob)!;
        var serverSetup = credentialBlob.ServerSetup;
        var serverRegistration = credentialBlob.PasswordFile;

        var loginResponse = _bitwardenOpaque.StartLogin(cipherConfiguration.ToNativeConfiguration(), serverSetup, serverRegistration, request, user.Id.ToString());
        var sessionId = Guid.NewGuid();
        SessionStore.LoginSessions.Add(sessionId, new LoginSession() { UserId = user.Id, LoginState = loginResponse.state, CipherConfiguration = cipherConfiguration });
        return (sessionId, loginResponse.credentialResponse);
    }

    public async Task<bool> FinishLogin(Guid sessionId, byte[] credentialFinalization)
    {
        return await Task.Run(async () =>
        {
            if (!SessionStore.LoginSessions.ContainsKey(sessionId))
            {
                throw new InvalidOperationException("Session not found");
            }

            var userId = SessionStore.LoginSessions[sessionId].UserId;
            var credential = await _opaqueKeyExchangeCredentialRepository.GetByUserIdAsync(userId);
            if (credential == null)
            {
                throw new InvalidOperationException("Credential not found");
            }

            var loginState = SessionStore.LoginSessions[sessionId].LoginState;
            var cipherConfiguration = SessionStore.LoginSessions[sessionId].CipherConfiguration;
            SessionStore.LoginSessions.Remove(sessionId);

            try
            {
                var success = _bitwardenOpaque.FinishLogin(cipherConfiguration.ToNativeConfiguration(), loginState, credentialFinalization);
                return true;
            }
            catch (Exception e)
            {
                // print
                Console.WriteLine(e.Message);
                return false;
            }
        });
    }

    public async Task SetActive(Guid sessionId, User user)
    {
        var session = SessionStore.RegisterSessions[sessionId];
        SessionStore.RegisterSessions.Remove(sessionId);

        if (session.UserId != user.Id)
        {
            throw new InvalidOperationException("Session does not belong to user");
        }
        if (session.PasswordFile == null)
        {
            throw new InvalidOperationException("Session did not complete registration");
        }
        if (session.KeySet == null)
        {
            throw new InvalidOperationException("Session did not complete registration");
        }

        var keyset = JsonSerializer.Deserialize<RotateableOpaqueKeyset>(Encoding.ASCII.GetString(session.KeySet))!;
        var credentialBlob = new OpaqueKeyExchangeCredentialBlob()
        {
            PasswordFile = session.PasswordFile,
            ServerSetup = session.ServerSetup
        };

        var credential = new OpaqueKeyExchangeCredential()
        {
            UserId = user.Id,
            CipherConfiguration = JsonSerializer.Serialize(session.CipherConfiguration),
            CredentialBlob = JsonSerializer.Serialize(credentialBlob),
            EncryptedPrivateKey = keyset.EncryptedPrivateKey,
            EncryptedPublicKey = keyset.EncryptedPublicKey,
            EncryptedUserKey = keyset.EncryptedUserKey,
            CreationDate = DateTime.UtcNow
        };

        await Unenroll(user);
        await _opaqueKeyExchangeCredentialRepository.CreateAsync(credential);
    }

    public async Task Unenroll(User user)
    {
        var credential = await _opaqueKeyExchangeCredentialRepository.GetByUserIdAsync(user.Id);
        if (credential != null)
        {
            await _opaqueKeyExchangeCredentialRepository.DeleteAsync(credential);
        }
    }
}

public class RegisterSession
{
    public required Guid SessionId { get; set; }
    public required byte[] ServerSetup { get; set; }
    public required Models.Api.Request.Opaque.CipherConfiguration CipherConfiguration { get; set; }
    public required Guid UserId { get; set; }
    public byte[]? PasswordFile { get; set; }
    public byte[]? KeySet { get; set; }
}

public class LoginSession
{
    public required Guid UserId { get; set; }
    public required byte[] LoginState { get; set; }
    public required Models.Api.Request.Opaque.CipherConfiguration CipherConfiguration { get; set; }
}

public class SessionStore()
{
    public static Dictionary<Guid, RegisterSession> RegisterSessions = new Dictionary<Guid, RegisterSession>();
    public static Dictionary<Guid, LoginSession> LoginSessions = new Dictionary<Guid, LoginSession>();
}
