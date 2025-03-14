using System.Text.Json;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Api.Request.Opaque;
using Bit.Core.Auth.Models.Api.Response.Opaque;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
using Bit.Core.Entities;
using Bitwarden.OPAQUE;
using Microsoft.Extensions.Caching.Distributed;

namespace Bit.Core.Auth.Services;

#nullable enable

public class OpaqueKeyExchangeService : IOpaqueKeyExchangeService
{
    private readonly BitwardenOpaqueServer _bitwardenOpaque;
    private readonly IOpaqueKeyExchangeCredentialRepository _opaqueKeyExchangeCredentialRepository;
    private readonly IDistributedCache _distributedCache;

    public OpaqueKeyExchangeService(
        IOpaqueKeyExchangeCredentialRepository opaqueKeyExchangeCredentialRepository,
        IDistributedCache distributedCache
        )
    {
        _bitwardenOpaque = new BitwardenOpaqueServer();
        _opaqueKeyExchangeCredentialRepository = opaqueKeyExchangeCredentialRepository;
        _distributedCache = distributedCache;
    }

    public async Task<OpaqueRegistrationStartResponse> StartRegistration(byte[] request, User user, Bitwarden.OPAQUE.CipherConfiguration cipherConfiguration)
    {
        var registrationRequest = _bitwardenOpaque.StartRegistration(cipherConfiguration, null, request, user.Id.ToString());
        var registrationReseponse = registrationRequest.registrationResponse;
        var serverSetup = registrationRequest.serverSetup;
        // persist server setup
        var sessionId = Guid.NewGuid();
        SessionStore.RegisterSessions.Add(sessionId, new RegisterSession() { SessionId = sessionId, ServerSetup = serverSetup, CipherConfiguration = cipherConfiguration, UserId = user.Id });
        await Task.Run(() => { });
        return new OpaqueRegistrationStartResponse(sessionId, Convert.ToBase64String(registrationReseponse));
    }

    public async void FinishRegistration(Guid sessionId, byte[] registrationUpload, User user, RotateableOpaqueKeyset keyset)
    {

        var currentSession = SessionStore.RegisterSessions[sessionId];

        var credentialBlob = new OpaqueKeyExchangeCredentialBlob()
        {
            PasswordFile = registrationUpload,
            ServerSetup = currentSession.ServerSetup
        };

        var credential = new OpaqueKeyExchangeCredential()
        {
            UserId = user.Id,
            CipherConfiguration = JsonSerializer.Serialize(currentSession.CipherConfiguration),
            CredentialBlob = JsonSerializer.Serialize(credentialBlob),
            EncryptedPrivateKey = keyset.EncryptedPrivateKey,
            EncryptedPublicKey = keyset.EncryptedPublicKey,
            EncryptedUserKey = keyset.EncryptedUserKey,
            CreationDate = DateTime.UtcNow
        };

        await _opaqueKeyExchangeCredentialRepository.CreateAsync(credential);
    }

    public async Task<(Guid, byte[])> StartLogin(byte[] request, string email)
    {
        return await Task.Run(() =>
        {
            var credential = PersistentStore.Credentials.First(x => x.Value.email == email);
            if (credential.Value == null)
            {
                // generate fake credential
                throw new InvalidOperationException("User not found");
            }

            var cipherConfiguration = credential.Value.cipherConfiguration;
            var serverSetup = credential.Value.serverSetup;
            var serverRegistration = credential.Value.serverRegistration;

            var loginResponse = _bitwardenOpaque.StartLogin(cipherConfiguration, serverSetup, serverRegistration, request, credential.Key.ToString());
            var sessionId = Guid.NewGuid();
            SessionStore.LoginSessions.Add(sessionId, new LoginSession() { UserId = credential.Key, LoginState = loginResponse.state });
            return (sessionId, loginResponse.credentialResponse);
        });
    }

    public async Task<bool> FinishLogin(Guid sessionId, byte[] credentialFinalization)
    {
        return await Task.Run(() =>
        {
            if (!SessionStore.LoginSessions.ContainsKey(sessionId))
            {
                throw new InvalidOperationException("Session not found");
            }
            var credential = PersistentStore.Credentials[SessionStore.LoginSessions[sessionId].UserId];
            if (credential == null)
            {
                throw new InvalidOperationException("User not found");
            }

            var loginState = SessionStore.LoginSessions[sessionId].LoginState;
            var cipherConfiguration = credential.cipherConfiguration;

            try
            {
                var success = _bitwardenOpaque.FinishLogin(cipherConfiguration, loginState, credentialFinalization);
                SessionStore.LoginSessions.Remove(sessionId);
                return true;
            }
            catch (Exception e)
            {
                // print
                Console.WriteLine(e.Message);
                SessionStore.LoginSessions.Remove(sessionId);
                return false;
            }
        });
    }

    public async void SetActive(Guid sessionId, User user)
    {
        await Task.Run(() =>
        {
            var session = SessionStore.RegisterSessions[sessionId];
            if (session.UserId != user.Id)
            {
                throw new InvalidOperationException("Session does not belong to user");
            }
            if (session.ServerRegistration == null)
            {
                throw new InvalidOperationException("Session did not complete registration");
            }
            SessionStore.RegisterSessions.Remove(sessionId);

            // to be copied to the persistent DB
            var cipherConfiguration = session.CipherConfiguration;
            var serverRegistration = session.ServerRegistration;
            var serverSetup = session.ServerSetup;

            if (PersistentStore.Credentials.ContainsKey(user.Id))
            {
                PersistentStore.Credentials.Remove(user.Id);
            }
            PersistentStore.Credentials.Add(user.Id, new Credential() { serverRegistration = serverRegistration, serverSetup = serverSetup, cipherConfiguration = cipherConfiguration, email = user.Email });
        });
    }

    public async void Unenroll(User user)
    {
        await Task.Run(() =>
        {
            PersistentStore.Credentials.Remove(user.Id);
        });
    }
}

public class RegisterSession
{
    public required Guid SessionId { get; set; }
    public required byte[] ServerSetup { get; set; }
    public required Bitwarden.OPAQUE.CipherConfiguration CipherConfiguration { get; set; }
    public required Guid UserId { get; set; }
    public byte[]? ServerRegistration { get; set; }
}

public class LoginSession
{
    public required Guid UserId { get; set; }
    public required byte[] LoginState { get; set; }
}

public class SessionStore()
{
    public static Dictionary<Guid, RegisterSession> RegisterSessions = new Dictionary<Guid, RegisterSession>();
    public static Dictionary<Guid, LoginSession> LoginSessions = new Dictionary<Guid, LoginSession>();
}

public class Credential
{
    public required byte[] serverRegistration { get; set; }
    public required byte[] serverSetup { get; set; }
    public required Bitwarden.OPAQUE.CipherConfiguration cipherConfiguration { get; set; }
    public required string email { get; set; }
}

public class PersistentStore()
{
    public static Dictionary<Guid, Credential> Credentials = new Dictionary<Guid, Credential>();
}
