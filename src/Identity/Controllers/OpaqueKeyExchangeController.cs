using Bit.Api.Auth.Models.Request.Opaque;
using Bit.Api.Auth.Models.Response.Opaque;
using Bit.Core;
using Bit.Core.Auth.Models.Api.Request.Opaque;
using Bit.Core.Auth.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Identity.Controllers;

[RequireFeature(FeatureFlagKeys.OpaqueKeyExchange)]
[Route("opaque-ke")]
public class OpaqueKeyExchangeController(
    IOpaqueKeyExchangeService opaqueKeyExchangeService
    ) : Controller
{
    private readonly IOpaqueKeyExchangeService _opaqueKeyExchangeService = opaqueKeyExchangeService;

    [HttpPost("start-login")]
    public async Task<OpaqueLoginStartResponse> StartOpaqueLoginAsync([FromBody] OpaqueLoginStartRequest request)
    {
        var result = await _opaqueKeyExchangeService.StartLogin(Convert.FromBase64String(request.CredentialRequest), request.Email);
        return new OpaqueLoginStartResponse(result.Item1, Convert.ToBase64String(result.Item2));
    }

    [HttpPost("finish-login")]
    public async Task<bool> FinishLoginAsync([FromBody] OpaqueLoginFinishRequest request)
    {
        var result = await _opaqueKeyExchangeService.FinishLogin(
            request.SessionId, Convert.FromBase64String(request.CredentialFinalization));
        return result;
    }
}
