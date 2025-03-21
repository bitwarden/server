using System.Text;
using System.Text.Json;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Api.Request.Opaque;
using Bit.Core.Auth.Models.Api.Response.Opaque;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
using Bit.Core.Auth.Utilities;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bitwarden.Opaque;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Auth.Services;

// TODO: add test suite
[RequireFeature(FeatureFlagKeys.OpaqueKeyExchange)]
public class OpaqueKeyExchangeService : IOpaqueKeyExchangeService
{
    private readonly BitwardenOpaqueServer _bitwardenOpaque;
    private readonly IOpaqueKeyExchangeCredentialRepository _opaqueKeyExchangeCredentialRepository;
    private readonly IDistributedCache _distributedCache;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<OpaqueKeyExchangeService> _logger;
    private readonly DistributedCacheEntryOptions _distributedCacheEntryOptions;
    private readonly byte[] _defaultKdfHmacKey = null;

    const string REGISTRATION_SESSION_KEY = "opaque_register_session_{0}";
    const string LOGIN_SESSION_KEY = "opaque_login_session_{0}";

    public OpaqueKeyExchangeService(
        IOpaqueKeyExchangeCredentialRepository opaqueKeyExchangeCredentialRepository,
        IDistributedCache distributedCache,
        IUserRepository userRepository,
        ILogger<OpaqueKeyExchangeService> logger,
        GlobalSettings globalSettings
        )
    {
        _bitwardenOpaque = new BitwardenOpaqueServer();
        _opaqueKeyExchangeCredentialRepository = opaqueKeyExchangeCredentialRepository;
        _distributedCache = distributedCache;
        _userRepository = userRepository;
        _logger = logger;
        _distributedCacheEntryOptions = new DistributedCacheEntryOptions()
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1)
        };
        if (CoreHelpers.SettingHasValue(globalSettings.KdfDefaultHashKey))
        {
            _defaultKdfHmacKey = Encoding.UTF8.GetBytes(globalSettings.KdfDefaultHashKey);
        }
        else
        {
            _defaultKdfHmacKey = new byte[32];
        }
    }

    public async Task<OpaqueRegistrationStartResponse> StartRegistration(
        byte[] request, User user, OpaqueKeyExchangeCipherConfiguration cipherConfiguration)
    {
        var registrationRequest = _bitwardenOpaque.StartRegistration(
            cipherConfiguration.ToNativeConfiguration(), null, request, user.Id.ToString());

        // We must persist the registration session state to the cache so we can have the server setup and cipher
        // configuration available when the client finishes registration.
        var registrationSessionId = Guid.NewGuid();
        var registrationSession = new OpaqueKeyExchangeRegistrationSession()
        {
            RegistrationSessionId = registrationSessionId,
            ServerSetup = registrationRequest.serverSetup,
            CipherConfiguration = cipherConfiguration,
            UserId = user.Id
        };

        await _distributedCache.SetAsync(
            string.Format(REGISTRATION_SESSION_KEY, registrationSessionId),
            Encoding.ASCII.GetBytes(JsonSerializer.Serialize(registrationSession)),
            _distributedCacheEntryOptions);

        return new OpaqueRegistrationStartResponse(registrationSessionId, Convert.ToBase64String(registrationRequest.registrationResponse));
    }

    public async Task<bool> FinishRegistration(Guid sessionId, byte[] registrationUpload, User user, RotateableOpaqueKeyset keyset)
    {
        try
        {
            // Look up the user's registration session
            var serializedRegisterSession = await _distributedCache.GetAsync(string.Format(REGISTRATION_SESSION_KEY, sessionId))
                ?? throw new Exception("Session not found");
            // Deserialize the registration session and finish the registration
            var registrationSession = JsonSerializer.Deserialize<OpaqueKeyExchangeRegistrationSession>(Encoding.ASCII.GetString(serializedRegisterSession))!;
            var registrationFinish = _bitwardenOpaque.FinishRegistration(registrationSession.CipherConfiguration.ToNativeConfiguration(), registrationUpload);
            // Save the keyset and password file to the registration session. In order to set their registration as
            // active, clients must call SetRegistrationActive or the user must change their password
            registrationSession.PasswordFile = registrationFinish.serverRegistration;
            registrationSession.KeySet = Encoding.ASCII.GetBytes(JsonSerializer.Serialize(keyset));
            await _distributedCache.SetAsync(
                string.Format(REGISTRATION_SESSION_KEY, sessionId),
                Encoding.ASCII.GetBytes(JsonSerializer.Serialize(registrationSession)),
                _distributedCacheEntryOptions);
            return true;
        }
        catch (Exception e)
        {
            // If anything goes wrong, we need to remove the session from the cache
            await ClearRegistrationSessionAsync(sessionId);
            _logger.LogError(e, "Error finishing registration for user {UserId}", user.Id);
            return false;
        }
    }

    private async Task ClearRegistrationSessionAsync(Guid sessionId)
    {
        await _distributedCache.RemoveAsync(string.Format(REGISTRATION_SESSION_KEY, sessionId));
    }

    public async Task<(Guid, byte[])> StartLogin(byte[] request, string email)
    {
        try
        {
            var user = await _userRepository.GetByEmailAsync(email);
            // Fake user to prevent user enumeration
            user ??= new User() { Id = Guid.Empty };

            byte[] serverSetup = null;
            byte[] serverRegistration = null;
            OpaqueKeyExchangeCipherConfiguration cipherConfiguration;
            var credential = await _opaqueKeyExchangeCredentialRepository.GetByUserIdAsync(user.Id);
            if (credential != null)
            {
                cipherConfiguration = JsonSerializer.Deserialize<OpaqueKeyExchangeCipherConfiguration>(credential.CipherConfiguration)!;
                var credentialBlob = JsonSerializer.Deserialize<OpaqueKeyExchangeCredentialBlob>(credential.CredentialBlob)!;
                serverSetup = credentialBlob.ServerSetup;
                serverRegistration = credentialBlob.PasswordFile;
            }
            else
            {
                // Generate a fake registration for non-existent users
                cipherConfiguration = new OpaqueKeyExchangeCipherConfiguration()
                {
                    CipherSuite = OpaqueKeyExchangeCipherConfiguration.OpaqueKe3Ristretto3DHArgonSuite,
                    Argon2Parameters = new Argon2KsfParameters()
                    {
                        Memory = 0,
                        Iterations = 0,
                        Parallelism = 0
                    }
                };
                var hmacMessage = Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant());
                using var hmac = new System.Security.Cryptography.HMACSHA256(_defaultKdfHmacKey);
                var hmacHash = hmac.ComputeHash(hmacMessage);
                (serverSetup, serverRegistration) = _bitwardenOpaque.SeededFakeRegistration(hmacHash);
            }

            var loginResponse = _bitwardenOpaque.StartLogin(
                cipherConfiguration.ToNativeConfiguration(), serverSetup, serverRegistration, request, user.Id.ToString());

            var sessionId = GuidUtilities.MakeCryptoGuid();
            var loginSession = new OpaqueKeyExchangeLoginSession()
            {
                UserId = user.Id,
                LoginState = loginResponse.state,
                CipherConfiguration = cipherConfiguration,
                IsAuthenticated = false
            };
            await _distributedCache.SetAsync(
                string.Format(LOGIN_SESSION_KEY, sessionId),
                Encoding.ASCII.GetBytes(JsonSerializer.Serialize(loginSession)),
                _distributedCacheEntryOptions);

            return (sessionId, loginResponse.credentialResponse);
        }
        catch (InvalidOperationException e)
        {
            _logger.LogError(e, "Error starting login for user {Email}", email);
            return (Guid.Empty, []);
        }
    }

    public async Task<bool> FinishLogin(Guid sessionId, byte[] credentialFinalization)
    {
        try
        {
            var serializedLoginSession = await _distributedCache.GetAsync(string.Format(LOGIN_SESSION_KEY, sessionId))
                ?? throw new InvalidOperationException("Session not found");
            var loginSession = JsonSerializer.Deserialize<OpaqueKeyExchangeLoginSession>(Encoding.ASCII.GetString(serializedLoginSession))!;

            var loginState = loginSession.LoginState;
            var cipherConfiguration = loginSession.CipherConfiguration;
            await ClearAuthenticationSession(sessionId);

            var success = _bitwardenOpaque.FinishLogin(cipherConfiguration.ToNativeConfiguration(), loginState, credentialFinalization);
            loginSession.IsAuthenticated = true;
            await _distributedCache.SetAsync(
                string.Format(LOGIN_SESSION_KEY, sessionId),
                Encoding.ASCII.GetBytes(JsonSerializer.Serialize(loginSession)),
                _distributedCacheEntryOptions);

            return true;
        }
        catch (InvalidOperationException e)
        {
            await ClearAuthenticationSession(sessionId);
            _logger.LogError(e, "Error finishing login for session {SessionId}", sessionId);
            return false;
        }
    }

    public async Task<User> GetUserForAuthenticatedSession(Guid sessionId)
    {
        try
        {
            var serializedLoginSession = await _distributedCache.GetAsync(string.Format(LOGIN_SESSION_KEY, sessionId))
                ?? throw new InvalidOperationException("Session not found");

            var loginSession = JsonSerializer.Deserialize<OpaqueKeyExchangeLoginSession>(Encoding.ASCII.GetString(serializedLoginSession))!;

            if (!loginSession.IsAuthenticated)
            {
                throw new InvalidOperationException("Session not authenticated");
            }

            return await _userRepository.GetByIdAsync(loginSession.UserId!)!;
        }
        catch (InvalidOperationException e)
        {
            _logger.LogError(e, "Error authenticating user session {SessionId}", sessionId);
            return null;
        }
    }

    public async Task<bool> WriteCacheCredentialToDatabase(Guid sessionId, User user)
    {
        try
        {
            var serializedRegisterSession = await _distributedCache.GetAsync(string.Format(REGISTRATION_SESSION_KEY, sessionId))
                ?? throw new InvalidOperationException("Session not found");

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
            await RemoveUserOpaqueKeyExchangeCredential(user);
            await _opaqueKeyExchangeCredentialRepository.CreateAsync(credential);
            return true;
        }
        catch (InvalidOperationException e)
        {
            _logger.LogError(e, "Error writing cache opaque credential to database for user {UserId}", user.Id);
            return false;
        }
    }

    public async Task RemoveUserOpaqueKeyExchangeCredential(User user)
    {
        var credential = await _opaqueKeyExchangeCredentialRepository.GetByUserIdAsync(user.Id);
        if (credential != null)
        {
            await _opaqueKeyExchangeCredentialRepository.DeleteAsync(credential);
        }
    }

    public async Task ClearAuthenticationSession(Guid sessionId)
    {
        await _distributedCache.RemoveAsync(string.Format(LOGIN_SESSION_KEY, sessionId));
    }
}

/// <summary>
/// Object saved to the cache for a registration session. We store the registration object in
/// the cache so we can maintain key material separation between the client and server.
/// If we used a Tokenable then it could expose the Server Key material to the client.
/// </summary>
public class OpaqueKeyExchangeRegistrationSession
{
    public required Guid RegistrationSessionId { get; set; }
    public required byte[] ServerSetup { get; set; }
    public required OpaqueKeyExchangeCipherConfiguration CipherConfiguration { get; set; }
    public required Guid UserId { get; set; }
    public byte[] PasswordFile { get; set; } = null;
    public byte[] KeySet { get; set; } = null;
}

/// <summary>
/// This object is used to accomplish a Pushed Authorization Request (PAR) "adjacent" type action. Where we
/// track authentication state in the cache so when a user finishes authentication they only need
/// the Cryptographically secure GUID sessionId.
/// </summary>
public class OpaqueKeyExchangeLoginSession
{
    public required Guid UserId { get; set; }
    public required byte[] LoginState { get; set; }
    public required OpaqueKeyExchangeCipherConfiguration CipherConfiguration { get; set; }
    public required bool IsAuthenticated { get; set; }
}
