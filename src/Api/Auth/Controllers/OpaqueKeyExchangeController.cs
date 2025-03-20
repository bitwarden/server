using Bit.Core;
using Bit.Core.Auth.Models.Api.Request.Opaque;
using Bit.Core.Auth.Models.Api.Response.Opaque;
using Bit.Core.Auth.Services;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Auth.Controllers;

[RequireFeature(FeatureFlagKeys.OpaqueKeyExchange)]
[Route("opaque")]
[Authorize("Web")]
public class OpaqueKeyExchangeController(
    IOpaqueKeyExchangeService opaqueKeyExchangeService,
    IUserService userService
    ) : Controller
{
    private readonly IOpaqueKeyExchangeService _opaqueKeyExchangeService = opaqueKeyExchangeService;
    private readonly IUserService _userService = userService;

    [HttpPost("start-registration")]
    public async Task<OpaqueRegistrationStartResponse> StartRegistrationAsync(
        [FromBody] OpaqueRegistrationStartRequest request)
    {
        var user = await _userService.GetUserByPrincipalAsync(User)
            ?? throw new UnauthorizedAccessException();
        var result = await _opaqueKeyExchangeService.StartRegistration(
            Convert.FromBase64String(request.RegistrationRequest), user, request.CipherConfiguration);
        return result;
    }

    [HttpPost("finish-registration")]
    public async Task FinishRegistrationAsync([FromBody] OpaqueRegistrationFinishRequest request)
    {
        var user = await _userService.GetUserByPrincipalAsync(User)
            ?? throw new UnauthorizedAccessException();
        await _opaqueKeyExchangeService.FinishRegistration(
            request.SessionId, Convert.FromBase64String(request.RegistrationUpload), user, request.KeySet);
    }

    [HttpPost("set-registration-active")]
    public async Task SetRegistrationActiveAsync([FromBody] OpaqueSetRegistrationActiveRequest request)
    {
        var user = await _userService.GetUserByPrincipalAsync(User)
            ?? throw new UnauthorizedAccessException();
        await _opaqueKeyExchangeService.WriteCacheCredentialToDatabase(request.SessionId, user);
    }
}
