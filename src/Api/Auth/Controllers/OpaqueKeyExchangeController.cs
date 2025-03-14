using Bit.Core.Auth.Models.Api.Request.Opaque;
using Bit.Core.Auth.Models.Api.Response.Opaque;
using Bit.Core.Auth.Services;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;

namespace Bit.Api.Auth.Controllers;

[Route("opaque")]
[Authorize("Web")]
public class OpaqueKeyExchangeController : Controller
{
    private readonly IOpaqueKeyExchangeService _opaqueKeyExchangeService;
    private readonly IUserService _userService;

    public OpaqueKeyExchangeController(
        IOpaqueKeyExchangeService opaqueKeyExchangeService,
        IUserService userService
    )
    {
        _opaqueKeyExchangeService = opaqueKeyExchangeService;
        _userService = userService;
    }

    [HttpPost("start-registration")]
    public async Task<OpaqueRegistrationStartResponse> StartRegistrationAsync([FromBody] OpaqueRegistrationStartRequest request)
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        var result = await _opaqueKeyExchangeService.StartRegistration(Convert.FromBase64String(request.RegistrationRequest), user, request.CipherConfiguration);
        return result;
    }


    [HttpPost("finish-registration")]
    public async Task<bool> FinishRegistration([FromBody] OpaqueRegistrationFinishRequest request)
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        var result = await _opaqueKeyExchangeService.FinishRegistration(request.SessionId, Convert.FromBase64String(request.RegistrationUpload), request.KeySet, user);
        return result;
    }
}
