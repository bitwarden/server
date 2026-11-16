#nullable enable

using Bit.Core;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.Services;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Core.Tokens;
using Bitwarden.Server.Sdk.Features;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Auth.Controllers;

/// <summary>
/// Authenticated endpoint the web vault calls after the user approves a "Login with Bitwarden" partner
/// consent prompt. Mints a short-lived, single-use consent token bound to the user and the pending authorize
/// request; the web vault then hands that token to Identity's consent callback to complete the flow. No PSK
/// is involved here.
/// </summary>
[Route("oidc")]
[Authorize("Application")]
public class OidcConsentController : Controller
{
    private readonly IUserService _userService;
    private readonly IOAuthConsentTokenableFactory _consentTokenableFactory;
    private readonly IDataProtectorTokenFactory<OAuthConsentTokenable> _consentTokenFactory;
    private readonly IOAuthConsentNonceStore _nonceStore;
    private readonly IFeatureService _featureService;

    public OidcConsentController(
        IUserService userService,
        IOAuthConsentTokenableFactory consentTokenableFactory,
        IDataProtectorTokenFactory<OAuthConsentTokenable> consentTokenFactory,
        IOAuthConsentNonceStore nonceStore,
        IFeatureService featureService)
    {
        _userService = userService;
        _consentTokenableFactory = consentTokenableFactory;
        _consentTokenFactory = consentTokenFactory;
        _nonceStore = nonceStore;
        _featureService = featureService;
    }

    [HttpPost("consent")]
    public async Task<OidcConsentResponseModel> PostConsent([FromBody] OidcConsentRequestModel model)
    {
        if (!_featureService.IsEnabled(FeatureFlagKeys.OpenIdConnectProvider))
        {
            throw new NotFoundException();
        }

        var userId = _userService.GetProperUserId(User);
        if (!userId.HasValue)
        {
            throw new UnauthorizedAccessException();
        }

        var nonce = await _nonceStore.IssueAsync();
        var tokenable = _consentTokenableFactory.CreateToken(userId.Value, model.ReturnUrl, nonce);
        var token = _consentTokenFactory.Protect(tokenable);

        return new OidcConsentResponseModel { Token = token };
    }
}

public class OidcConsentRequestModel
{
    /// <summary>The <c>returnUrl</c> of the pending authorize request, from Identity's login redirect.</summary>
    public required string ReturnUrl { get; set; }
}

public class OidcConsentResponseModel
{
    /// <summary>The protected, single-use consent token to hand to Identity's consent callback.</summary>
    public required string Token { get; set; }
}
