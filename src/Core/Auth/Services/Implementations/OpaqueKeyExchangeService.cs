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
using Bitwarden.Opaque;
using Microsoft.Extensions.Caching.Distributed;

namespace Bit.Core.Auth.Services;

#nullable enable

// TODO: feature flag this service
// TODO: add test suite
public class OpaqueKeyExchangeService : IOpaqueKeyExchangeService
{
    private readonly BitwardenOpaqueServer _bitwardenOpaque;
    private readonly IOpaqueKeyExchangeCredentialRepository _opaqueKeyExchangeCredentialRepository;
    private readonly IDistributedCache _distributedCache;
    private readonly IUserRepository _userRepository;

    const string REGISTRATION_SESSION_KEY = "opaque_register_session_{0}";
    const string LOGIN_SESSION_KEY = "opaque_login_session_{0}";

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

    public async Task<OpaqueRegistrationStartResponse> StartRegistration(byte[] request, User user, OpaqueKeyExchangeCipherConfiguration cipherConfiguration)
    {
        var registrationRequest = _bitwardenOpaque.StartRegistration(cipherConfiguration.ToNativeConfiguration(), null, request, user.Id.ToString());

        // We must persist the registration session state to the cache so we can have the server setup and cipher
        // Ïconfiguration available when the client finishes registration.
        var registrationSessionId = Guid.NewGuid();
        var registrationSession = new OpaqueKeyExchangeRegistrationSession() { RegistrationSessionId = registrationSessionId, ServerSetup = registrationRequest.serverSetup, CipherConfiguration = cipherConfiguration, UserId = user.Id };

        // TODO: We need to audit this approach to make sure it works for all our use cases. We probably need a timeout to avoid persisting this indefinitely.
        await _distributedCache.SetAsync(string.Format(REGISTRATION_SESSION_KEY, registrationSessionId), Encoding.ASCII.GetBytes(JsonSerializer.Serialize(registrationSession)));

        return new OpaqueRegistrationStartResponse(registrationSessionId, Convert.ToBase64String(registrationRequest.registrationResponse));
    }

    public async Task FinishRegistration(Guid sessionId, byte[] registrationUpload, User user, RotateableOpaqueKeyset keyset)
    {
        // Look up the user's registration session
        var serializedRegisterSession = await _distributedCache.GetAsync(string.Format(REGISTRATION_SESSION_KEY, sessionId));
        if (serializedRegisterSession == null)
        {
            throw new InvalidOperationException("Session not found");
        }

        try
        {
            // Deserialize the registration session and finish the registration
            var registrationSession = JsonSerializer.Deserialize<OpaqueKeyExchangeRegistrationSession>(Encoding.ASCII.GetString(serializedRegisterSession))!;
            var registrationFinish = _bitwardenOpaque.FinishRegistration(registrationSession.CipherConfiguration.ToNativeConfiguration(), registrationUpload);
            // Save the keyset and password file to the registration session. In order to set their registration as
            // active, clients must call SetRegistrationActive or the user must change their password
            registrationSession.PasswordFile = registrationFinish.serverRegistration;
            registrationSession.KeySet = Encoding.ASCII.GetBytes(JsonSerializer.Serialize(keyset));
            await _distributedCache.SetAsync(string.Format(REGISTRATION_SESSION_KEY, sessionId), Encoding.ASCII.GetBytes(JsonSerializer.Serialize(registrationSession)));
        }
        catch (Exception e)
        {
            // If anything goes wrong, we need to remove the session from the cache
            await ClearRegistrationSession(sessionId);
            throw new Exception(e.Message);
        }
    }

    private async Task ClearRegistrationSession(Guid sessionId)
    {
        await _distributedCache.RemoveAsync(string.Format(REGISTRATION_SESSION_KEY, sessionId));
    }

    public async Task<(Guid, byte[])> StartLogin(byte[] request, string email)
    {
        var user = await _userRepository.GetByEmailAsync(email);
        if (user == null)
        {
            // TODO: don't allow user enumeration
            throw new InvalidOperationException("User not found");
        }

        var credential = await _opaqueKeyExchangeCredentialRepository.GetByUserIdAsync(user.Id);
        if (credential == null)
        {
            // generate fake credential
            throw new InvalidOperationException("Credential not found");
        }

        var cipherConfiguration = JsonSerializer.Deserialize<OpaqueKeyExchangeCipherConfiguration>(credential.CipherConfiguration)!;
        var credentialBlob = JsonSerializer.Deserialize<OpaqueKeyExchangeCredentialBlob>(credential.CredentialBlob)!;
        var serverSetup = credentialBlob.ServerSetup;
        var serverRegistration = credentialBlob.PasswordFile;

        var loginResponse = _bitwardenOpaque.StartLogin(cipherConfiguration.ToNativeConfiguration(), serverSetup, serverRegistration, request, user.Id.ToString());
        var sessionId = MakeCryptoGuid();
        var loginSession = new OpaqueKeyExchangeLoginSession()
        {
            UserId = user.Id,
            LoginState = loginResponse.state,
            CipherConfiguration = cipherConfiguration,
            IsAuthenticated = false
        };
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
        var loginSession = JsonSerializer.Deserialize<OpaqueKeyExchangeLoginSession>(Encoding.ASCII.GetString(serializedLoginSession))!;

        var credential = await _opaqueKeyExchangeCredentialRepository.GetByUserIdAsync(loginSession.UserId);
        if (credential == null)
        {
            throw new InvalidOperationException("Credential not found");
        }

        var loginState = loginSession.LoginState;
        var cipherConfiguration = loginSession.CipherConfiguration;
        await ClearAuthenticationSession(sessionId);

        try
        {
            var success = _bitwardenOpaque.FinishLogin(cipherConfiguration.ToNativeConfiguration(), loginState, credentialFinalization);
            loginSession.IsAuthenticated = true;
            await _distributedCache.SetAsync(string.Format(LOGIN_SESSION_KEY, sessionId), Encoding.ASCII.GetBytes(JsonSerializer.Serialize(loginSession)));
            return true;
        }
        catch (Exception e)
        {
            // print
            Console.WriteLine(e.Message);
            return false;
        }
    }

    public async Task<User?> GetUserForAuthenticatedSession(Guid sessionId)
    {
        var serializedLoginSession = await _distributedCache.GetAsync(string.Format(LOGIN_SESSION_KEY, sessionId));
        if (serializedLoginSession == null)
        {
            throw new InvalidOperationException("Session not found");
        }
        var loginSession = JsonSerializer.Deserialize<OpaqueKeyExchangeLoginSession>(Encoding.ASCII.GetString(serializedLoginSession))!;

        if (!loginSession.IsAuthenticated)
        {
            throw new InvalidOperationException("Session not authenticated");
        }

        return await _userRepository.GetByIdAsync(loginSession.UserId!)!;
    }

    public async Task SetRegistrationActiveForAccount(Guid sessionId, User user)
    {
        var serializedRegisterSession = await _distributedCache.GetAsync(string.Format(REGISTRATION_SESSION_KEY, sessionId));
        if (serializedRegisterSession == null)
        {
            throw new InvalidOperationException("Session not found");
        }
        var session = JsonSerializer.Deserialize<OpaqueKeyExchangeRegistrationSession>(Encoding.ASCII.GetString(serializedRegisterSession))!;

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

        // Delete any existing registration and then enroll user with latest
        // TODO: this could be a single atomic replace / upsert
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

    // TODO: evaluate if this should live in a utility class and/or under KM control
    /// <summary>
    /// Creates a genuinely random GUID as the login session IDs for opaque need to be cryptographically secure.
    /// </summary>
    private static Guid MakeCryptoGuid()
    {
        // Get 16 cryptographically random bytes
        byte[] data = RandomNumberGenerator.GetBytes(16);

        // Mark it as a version 4 GUID
        data[7] = (byte)((data[7] | (byte)0x40) & (byte)0x4f);
        data[8] = (byte)((data[8] | (byte)0x80) & (byte)0xbf);

        return new Guid(data);
    }

    public async Task ClearAuthenticationSession(Guid sessionId)
    {
        await _distributedCache.RemoveAsync(string.Format(LOGIN_SESSION_KEY, sessionId));
    }
}

public class OpaqueKeyExchangeRegistrationSession
{
    public required Guid RegistrationSessionId { get; set; }
    public required byte[] ServerSetup { get; set; }
    public required OpaqueKeyExchangeCipherConfiguration CipherConfiguration { get; set; }
    public required Guid UserId { get; set; }
    public byte[]? PasswordFile { get; set; }
    public byte[]? KeySet { get; set; }
}

public class OpaqueKeyExchangeLoginSession
{
    public required Guid UserId { get; set; }
    public required byte[] LoginState { get; set; }
    public required OpaqueKeyExchangeCipherConfiguration CipherConfiguration { get; set; }
    public required bool IsAuthenticated { get; set; }
}
