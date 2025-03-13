using Bit.Core.Entities;
using Bitwarden.OPAQUE;

namespace Bit.Core.Auth.Services;

public class OpaqueKeyExchangeService : IOpaqueKeyExchangeService
{

    private readonly BitwardenOpaqueServer _bitwardenOpaque;

    public OpaqueKeyExchangeService(
    )
    {
        _bitwardenOpaque = new BitwardenOpaqueServer();
    }


    public async Task<(Guid, byte[])> StartRegistration(byte[] request, User user, CipherConfiguration cipherConfiguration)
    {
        var registrationRequest = _bitwardenOpaque.StartRegistration(cipherConfiguration, null, request, user.Id.ToString());
        var message = registrationRequest.registrationResponse;
        var serverSetup = registrationRequest.serverSetup;
        // persist server setup
        var sessionId = Guid.NewGuid();
        SessionStore.RegisterSessions.Add(sessionId, new RegisterSession() { SessionId = sessionId, ServerSetup = serverSetup, cipherConfiguration = cipherConfiguration });
        await Task.Run(() => { });
        return (sessionId, message);
    }

    public async Task<bool> FinishRegistration(Guid sessionId, byte[] request, User user)
    {
        await Task.Run(() => { });
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
