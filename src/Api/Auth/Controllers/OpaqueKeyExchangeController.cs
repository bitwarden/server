using Bit.Api.Auth.Models.Request.Opaque;
using Bit.Api.Auth.Models.Response.Opaque;
using Bit.Core.Services;
using Bitwarden.OPAQUE;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Auth.Controllers;

[Route("opaque")]
[Authorize("Web")]
public class OpaqueKeyExchangeController : Controller
{
    private readonly IUserService _userService;
    private readonly BitwardenOpaque _bitwardenOpaque;
    private CipherConfiguration _cipherConfiguration = new CipherConfiguration();

    public OpaqueKeyExchangeController(
        IUserService userService
    )
    {
        _userService = userService;
        _bitwardenOpaque = new BitwardenOpaque();
        _cipherConfiguration.KeGroup = KeGroup.Ristretto255;
        _cipherConfiguration.OprfCS = OprfCS.Ristretto255;
        _cipherConfiguration.KeyExchange = KeyExchange.TripleDH;
        _cipherConfiguration.KSF = new Argon2id(3, 256 * 1024, 4);
    }

    [HttpPost("~/opaque/start-registration")]
    public async Task<RegisterStartResponse> StartRegistration([FromBody] RegisterStartRequest request)
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        var registrationRequest = _bitwardenOpaque.StartServerRegistration(_cipherConfiguration, System.Convert.FromBase64String(request.ClientRegistrationStartResult), user.Id.ToString());
        var message = registrationRequest.Item1;
        var serverSetup = registrationRequest.Item2;
        // persist server setup
        var sessionId = Guid.NewGuid();
        SessionStore.RegisterSessions.Add(sessionId, new RegisterSession() { SessionId = sessionId, ServerSetup = serverSetup, cipherConfiguration = _cipherConfiguration });
        return new RegisterStartResponse(sessionId, System.Convert.ToBase64String(message));
    }


    [HttpPost("~/opaque/finish-registration")]
    public async Task<String> FinishRegistration([FromBody] RegisterFinishRequest request)
    {
        await Task.Run(() =>
        {
            var registrationFinish = _bitwardenOpaque.FinishServerRegistration(_cipherConfiguration, System.Convert.FromBase64String(request.ClientRegistrationFinishResult));
            Console.WriteLine("Registration Finish: " + registrationFinish);
        });
        return "Registration Finish";
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
