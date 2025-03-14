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

public class OpaqueKeyExchangeService : IOpaqueKeyExchangeService
{
    private readonly BitwardenOpaqueServer _bitwardenOpaque;
    private readonly IOpaqueKeyExchangeCredentialRepository _opaqueKeyExchangeCredentialRepository;
    private readonly IDistributedCache _distributedCache;

    static string _opaqueRegistrationCacheKeyFormat = "OPAQUE-KE-Registration_{0}";

    public OpaqueKeyExchangeService(
        IOpaqueKeyExchangeCredentialRepository opaqueKeyExchangeCredentialRepository,
        IDistributedCache distributedCache
        )
    {
        _bitwardenOpaque = new BitwardenOpaqueServer();
        _opaqueKeyExchangeCredentialRepository = opaqueKeyExchangeCredentialRepository;
        _distributedCache = distributedCache;
    }


    public async Task<OpaqueRegistrationStartResponse> StartRegistration(byte[] request, User user, CipherConfiguration cipherConfiguration)
    {
        var registrationRequest = _bitwardenOpaque.StartRegistration(cipherConfiguration, null, request, user.Id.ToString());
        var registrationReseponse = registrationRequest.registrationResponse;
        var serverSetup = registrationRequest.serverSetup;
        // persist server setup
        var sessionId = Guid.NewGuid();
        SessionStore.RegisterSessions.Add(sessionId, new RegisterSession() { SessionId = sessionId, ServerSetup = serverSetup, cipherConfiguration = cipherConfiguration });
        await Task.Run(() => { });
        return new OpaqueRegistrationStartResponse(sessionId, Convert.ToBase64String(registrationReseponse));
    }

    public async Task<bool> FinishRegistration(Guid sessionId, byte[] clientSetup, RotateableOpaqueKeyset keyset, User user)
    {
        var currentSession = SessionStore.RegisterSessions[sessionId];

        var credentialBlob = new OpaqueKeyExchangeCredentialBlob()
        {
            ClientSetup = clientSetup,
            ServerSetup = currentSession.ServerSetup
        };

        var credential = new OpaqueKeyExchangeCredential()
        {
            UserId = user.Id,
            CipherConfiguration = JsonSerializer.Serialize(currentSession.cipherConfiguration),
            CredentialBlob = JsonSerializer.Serialize(credentialBlob),
            EncryptedPrivateKey = keyset.EncryptedPrivateKey,
            EncryptedPublicKey = keyset.EncryptedPublicKey,
            EncryptedUserKey = keyset.EncryptedUserKey,
            CreationDate = DateTime.UtcNow
        };

        await _opaqueKeyExchangeCredentialRepository.CreateAsync(credential);
        return true;
    }
}

public class RegisterSession
{
    public Guid SessionId { get; set; }
    public byte[] ServerSetup { get; set; }
    public CipherConfiguration cipherConfiguration { get; set; }
}

public class SessionStore()
{
    public static Dictionary<Guid, RegisterSession> RegisterSessions = new Dictionary<Guid, RegisterSession>();
    public static Dictionary<Guid, byte[]> LoginSessions = new Dictionary<Guid, byte[]>();
}
