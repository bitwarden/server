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
    private readonly BitwardenOpaqueServer _bitwardenOpaque;

    public OpaqueKeyExchangeController(
        IUserService userService
    )
    {
        _userService = userService;
        _bitwardenOpaque = new BitwardenOpaqueServer();
    }

    [HttpPost("~/opaque/start-registration")]
    public async Task<OpaqueRegistrationStartResponse> StartRegistration([FromBody] OpaqueRegistrationStartRequest request)
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        var registrationRequest = _bitwardenOpaque.StartRegistration(request.CipherConfiguration, null, System.Convert.FromBase64String(request.RegistrationRequest), user.Id.ToString());
        var message = registrationRequest.registrationResponse;
        var serverSetup = registrationRequest.serverSetup;
        // persist server setup
        var sessionId = Guid.NewGuid();
        SessionStore.RegisterSessions.Add(sessionId, new RegisterSession() { SessionId = sessionId, ServerSetup = serverSetup, cipherConfiguration = request.CipherConfiguration });
        return new OpaqueRegistrationStartResponse(sessionId, System.Convert.ToBase64String(message));
    }


    [HttpPost("~/opaque/finish-registration")]
    public async Task<String> FinishRegistration([FromBody] OpaqueRegistrationFinishRequest request)
    {
        await Task.Run(() =>
        {
            var registerSession = SessionStore.RegisterSessions[request.SessionId];
            var registrationFinish = _bitwardenOpaque.FinishRegistration(registerSession.cipherConfiguration, System.Convert.FromBase64String(request.RegistrationUpload));
            Console.WriteLine("Registration Finish: " + registrationFinish);
        });
        return "";
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
