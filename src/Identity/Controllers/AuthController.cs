using System;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Bit.Core.Utilities;
using IdentityServer4.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Bit.Identity.Controllers
{
    public class AuthController : Controller
    {
        private readonly IIdentityServerInteractionService _interaction;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            IIdentityServerInteractionService interaction,
            ILogger<AuthController> logger)
        {
            _interaction = interaction;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Challenge(string organizationIdentifier, string returnUrl, string returnType)
        {
            if (string.IsNullOrWhiteSpace(organizationIdentifier))
            {
                throw new Exception("Invalid organization reference id.");
            }

            // TODO: Lookup sso config and create a domain hint
            var domainHint = "oidc_okta";
            // Temp hardcoded orgs
            if (organizationIdentifier == "org_oidc_okta")
            {
                domainHint = "oidc_okta";
            }
            else if (organizationIdentifier == "org_oidc_onelogin")
            {
                domainHint = "oidc_onelogin";
            }
            else if (organizationIdentifier == "org_saml2_onelogin")
            {
                domainHint = "saml2_onelogin";
            }
            else if (organizationIdentifier == "org_saml2_sustainsys")
            {
                domainHint = "saml2_sustainsys";
            }
            else
            {
                throw new Exception("Organization not found.");
            }

            var provider = "sso";
            var props = new AuthenticationProperties
            {
                RedirectUri = Url.Action(nameof(Callback)),
                Items =
                {
                    { "return_url", returnUrl },
                    { "return_type", returnType },
                    { "domain_hint", domainHint },
                    { "scheme", provider },
                },
            };

            return Challenge(props, provider);
        }

        [HttpGet]
        public async Task<ActionResult> Callback()
        {
            // Read external identity from the temporary cookie
            var result = await HttpContext.AuthenticateAsync(
                IdentityServer4.IdentityServerConstants.ExternalCookieAuthenticationScheme);
            if (result?.Succeeded != true)
            {
                throw new Exception("External authentication error");
            }

            // Debugging
            var externalClaims = result.Principal.Claims.Select(c => $"{c.Type}: {c.Value}");
            _logger.LogDebug("External claims: {@claims}", externalClaims);

            var accessToken = await HttpContext.GetTokenAsync("access_token");
            var expiresAt = await HttpContext.GetTokenAsync("expires_at");
            var refreshToken = await HttpContext.GetTokenAsync("refresh_token");

            // Delete temporary cookie used during external authentication
            await HttpContext.SignOutAsync(IdentityServer4.IdentityServerConstants.ExternalCookieAuthenticationScheme);

            if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(refreshToken))
            {
                throw new Exception("Tokens not available.");
            }

            // Parse expires at date
            DateTime.TryParse(expiresAt, out var expiresAtDate);

            // Retrieve return url
            var returnType = result.Properties.Items["return_type"] ?? "redirect";
            var returnUrl = result.Properties.Items["return_url"];
            if (string.IsNullOrWhiteSpace(returnUrl))
            {
                returnUrl = "~/";
            }
            if (Url.IsLocalUrl(returnUrl) && returnType == "redirect-params")
            {
                returnType = "redirect";
            }

            // check if external login is in the context of an OIDC request
            var context = await _interaction.GetAuthorizationContextAsync(returnUrl);
            if (context != null)
            {
                // TODO: Check if pkce?
            }

            // TODO: What kind of results do we want here?
            if (returnType == "json")
            {
                return new JsonResult(new
                {
                    access_token = accessToken,
                    exp = CoreHelpers.ToEpocSeconds(expiresAtDate),
                    refresh_token = refreshToken
                });
            }
            else if (returnType == "redirect-params")
            {
                var uriBuilder = new UriBuilder(returnUrl);
                var query = HttpUtility.ParseQueryString(uriBuilder.Query);
                query["accessToken"] = accessToken;
                query["expiresAt"] = CoreHelpers.ToEpocSeconds(expiresAtDate).ToString();
                query["refreshToken"] = refreshToken;
                uriBuilder.Query = query.ToString();
                return Redirect(uriBuilder.ToString());
            }
            else
            {
                return Redirect(returnUrl);
            }
        }
    }
}
