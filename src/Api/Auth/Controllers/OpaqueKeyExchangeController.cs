using Bit.Api.Auth.Models.Request.Opaque;
using Bit.Api.Auth.Models.Response.Opaque;
using Bit.Core.Auth.Services;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Auth.Controllers;

[Route("opaque")]
[Authorize("Web")]
public class OpaqueKeyExchangeController : Controller
{
    private readonly IOpaqueKeyExchangeService _opaqueKeyExchangeService;
    IUserService _userService;

    public OpaqueKeyExchangeController(
        IOpaqueKeyExchangeService opaqueKeyExchangeService,
        IUserService userService
    )
    {
        _opaqueKeyExchangeService = opaqueKeyExchangeService;
        _userService = userService;
    }

    [HttpPost("~/opaque/start-registration")]
    public async Task<OpaqueRegistrationStartResponse> StartRegistration([FromBody] OpaqueRegistrationStartRequest request)
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        var result = await _opaqueKeyExchangeService.StartRegistration(System.Convert.FromBase64String(request.RegistrationRequest), user, request.CipherConfiguration);
        return new OpaqueRegistrationStartResponse(result.Item1, System.Convert.ToBase64String(result.Item2));
    }


    [HttpPost("~/opaque/finish-registration")]
    public async Task<String> FinishRegistration([FromBody] OpaqueRegistrationFinishRequest request)
    {
        await Task.Run(() => { });
        return "";
    }

}

