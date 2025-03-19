using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Api.Request.Opaque;
using Bit.Core.Auth.Models.Api.Response.Opaque;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bitwarden.Opaque;
using Microsoft.Extensions.Caching.Distributed;

namespace Bit.Core.Auth.Services;

#nullable enable

public class OpaqueKeyExchangeService : IOpaqueKeyExchangeService
{
    private readonly BitwardenOpaqueServer _bitwardenOpaque;
    private readonly IOpaqueKeyExchangeCredentialRepository _opaqueKeyExchangeCredentialRepository;
    private readonly IDistributedCache _distributedCache;
    private readonly IUserRepository _userRepository;
    private readonly byte[]? _defaultKdfHmacKey;

    const string REGISTER_SESSION_KEY = "opaque_register_session_{0}";
    const string LOGIN_SESSION_KEY = "opaque_login_session_{0}";

    public OpaqueKeyExchangeService(
        IOpaqueKeyExchangeCredentialRepository opaqueKeyExchangeCredentialRepository,
        IDistributedCache distributedCache,
        IUserRepository userRepository,
        GlobalSettings globalSettings
        )
    {
        _bitwardenOpaque = new BitwardenOpaqueServer();
        _opaqueKeyExchangeCredentialRepository = opaqueKeyExchangeCredentialRepository;
        _distributedCache = distributedCache;
        _userRepository = userRepository;
        if (CoreHelpers.SettingHasValue(globalSettings.KdfDefaultHashKey))
        {
            _defaultKdfHmacKey = Encoding.UTF8.GetBytes(globalSettings.KdfDefaultHashKey);
        }
        else
        {
            _defaultKdfHmacKey = new byte[32];
        }
    }

    public async Task<OpaqueRegistrationStartResponse> StartRegistration(byte[] request, User user, Models.Api.Request.Opaque.CipherConfiguration cipherConfiguration)
    {
        var registrationRequest = _bitwardenOpaque.StartRegistration(cipherConfiguration.ToNativeConfiguration(), null, request, user.Id.ToString());

        var sessionId = Guid.NewGuid();
        var registerSession = new RegisterSession() { SessionId = sessionId, ServerSetup = registrationRequest.serverSetup, CipherConfiguration = cipherConfiguration, UserId = user.Id };
        await _distributedCache.SetAsync(string.Format(REGISTER_SESSION_KEY, sessionId), Encoding.ASCII.GetBytes(JsonSerializer.Serialize(registerSession)));

        return new OpaqueRegistrationStartResponse(sessionId, Convert.ToBase64String(registrationRequest.registrationResponse));
    }

    public async Task FinishRegistration(Guid sessionId, byte[] registrationUpload, User user, RotateableOpaqueKeyset keyset)
    {
        var serializedRegisterSession = await _distributedCache.GetAsync(string.Format(REGISTER_SESSION_KEY, sessionId));
        if (serializedRegisterSession == null)
        {
            throw new InvalidOperationException("Session not found");
        }

        try
        {
            var registerSession = JsonSerializer.Deserialize<RegisterSession>(Encoding.ASCII.GetString(serializedRegisterSession))!;
            var registrationFinish = _bitwardenOpaque.FinishRegistration(registerSession.CipherConfiguration.ToNativeConfiguration(), registrationUpload);
            registerSession.PasswordFile = registrationFinish.serverRegistration;
            registerSession.KeySet = Encoding.ASCII.GetBytes(JsonSerializer.Serialize(keyset));
            await _distributedCache.SetAsync(string.Format(REGISTER_SESSION_KEY, sessionId), Encoding.ASCII.GetBytes(JsonSerializer.Serialize(registerSession)));
        }
        catch (Exception e)
        {
            await _distributedCache.RemoveAsync(string.Format(REGISTER_SESSION_KEY, sessionId));
            throw new Exception(e.Message);
        }
    }

    public async Task<(Guid, byte[])> StartLogin(byte[] request, string email)
    {
        var user = await _userRepository.GetByEmailAsync(email);
        if (user == null)
        {
            user = new User();
            user.Id = Guid.Empty;
        }


        var credential = await _opaqueKeyExchangeCredentialRepository.GetByUserIdAsync(user.Id);
        byte[]? serverSetup = null;
        byte[]? serverRegistration = null;
        Models.Api.Request.Opaque.CipherConfiguration? cipherConfiguration = null;
        if (credential == null)
        {
            using var hmac = new HMACSHA256(_defaultKdfHmacKey);
            var hmacHash = hmac.ComputeHash(Encoding.ASCII.GetBytes(email));
            (serverSetup, serverRegistration) = _bitwardenOpaque.SeededFakeRegistration(hmacHash);
            cipherConfiguration = new Models.Api.Request.Opaque.CipherConfiguration
            {
                CipherSuite = Models.Api.Request.Opaque.CipherConfiguration.OpaqueKe3Ristretto3DHArgonSuite,
                Argon2Parameters = new Argon2KsfParameters
                {
                    Memory = 0,
                    Iterations = 0,
                    Parallelism = 0
                }
            };
        }
        else
        {
            var credentialBlob = JsonSerializer.Deserialize<OpaqueKeyExchangeCredentialBlob>(credential.CredentialBlob)!;
            serverSetup = credentialBlob.ServerSetup;
            serverRegistration = credentialBlob.PasswordFile;
            cipherConfiguration = JsonSerializer.Deserialize<Models.Api.Request.Opaque.CipherConfiguration>(credential.CipherConfiguration)!;
        }

        var loginResponse = _bitwardenOpaque.StartLogin(cipherConfiguration.ToNativeConfiguration(), serverSetup, serverRegistration, request, user.Id.ToString());
        var sessionId = Guid.NewGuid();
        var loginSession = new LoginSession() { UserId = user.Id, LoginState = loginResponse.state, CipherConfiguration = cipherConfiguration };
        await _distributedCache.SetAsync(string.Format(LOGIN_SESSION_KEY, sessionId), Encoding.ASCII.GetBytes(JsonSerializer.Serialize(loginSession)));
        return (sessionId, loginResponse.credentialResponse);
    }

    public async Task<bool> FinishLogin(Guid sessionId, byte[] credentialFinalization)
    {
        var serializedLoginSession = await _distributedCache.GetAsync(string.Format(LOGIN_SESSION_KEY, sessionId));
        if (serializedLoginSession == null)
        {
            throw new InvalidOperationException("Session not found");
        }
        var loginSession = JsonSerializer.Deserialize<LoginSession>(Encoding.ASCII.GetString(serializedLoginSession))!;

        var credential = await _opaqueKeyExchangeCredentialRepository.GetByUserIdAsync(loginSession.UserId);
        if (credential == null)
        {
            throw new InvalidOperationException("Credential not found");
        }

        var loginState = loginSession.LoginState;
        var cipherConfiguration = loginSession.CipherConfiguration;
        await _distributedCache.RemoveAsync(string.Format(LOGIN_SESSION_KEY, sessionId));

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
    }

    public async Task SetActive(Guid sessionId, User user)
    {
        var serializedRegisterSession = await _distributedCache.GetAsync(string.Format(REGISTER_SESSION_KEY, sessionId));
        if (serializedRegisterSession == null)
        {
            throw new InvalidOperationException("Session not found");
        }
        var session = JsonSerializer.Deserialize<RegisterSession>(Encoding.ASCII.GetString(serializedRegisterSession))!;

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
