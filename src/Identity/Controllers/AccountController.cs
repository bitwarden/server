using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;
using Bit.Identity.Models;
using IdentityModel;
using IdentityServer4;
using IdentityServer4.Services;
using IdentityServer4.Stores;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Bit.Identity.Controllers
{
    public class AccountController : Controller
    {
        private readonly IIdentityServerInteractionService _interaction;
        private readonly IUserRepository _userRepository;
        private readonly IClientStore _clientStore;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            IIdentityServerInteractionService interaction,
            IUserRepository userRepository,
            IClientStore clientStore,
            ILogger<AccountController> logger)
        {
            _interaction = interaction;
            _userRepository = userRepository;
            _clientStore = clientStore;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Login(string returnUrl)
        {
            var context = await _interaction.GetAuthorizationContextAsync(returnUrl);
            if (context.Parameters.AllKeys.Contains("domain_hint") &&
                !string.IsNullOrWhiteSpace(context.Parameters["domain_hint"]))
            {
                return RedirectToAction(nameof(ExternalChallenge),
                    new { organizationIdentifier = context.Parameters["domain_hint"], returnUrl = returnUrl });
            }
            else
            {
                throw new Exception("No domain_hint provided.");
            }
        }

        [HttpGet]
        public IActionResult ExternalChallenge(string organizationIdentifier, string returnUrl)
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
                RedirectUri = Url.Action(nameof(ExternalCallback)),
                Items =
                {
                    { "return_url", returnUrl },
                    { "domain_hint", domainHint },
                    { "scheme", provider },
                },
            };

            return Challenge(props, provider);
        }

        [HttpGet]
        public async Task<ActionResult> ExternalCallback()
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

            var (user, provider, providerUserId, claims) = await FindUserFromExternalProviderAsync(result);
            if (user == null)
            {
                // Should never happen
                throw new Exception("Cannot find user.");
            }

            // this allows us to collect any additonal claims or properties
            // for the specific prtotocols used and store them in the local auth cookie.
            // this is typically used to store data needed for signout from those protocols.
            var additionalLocalClaims = new List<Claim>();
            var localSignInProps = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(1)
            };
            ProcessLoginCallbackForOidc(result, additionalLocalClaims, localSignInProps);

            // issue authentication cookie for user
            await HttpContext.SignInAsync(new IdentityServerUser(user.Id.ToString())
            {
                DisplayName = user.Email,
                IdentityProvider = provider,
                AdditionalClaims = additionalLocalClaims.ToArray()
            }, localSignInProps);

            // delete temporary cookie used during external authentication
            await HttpContext.SignOutAsync(IdentityServer4.IdentityServerConstants.ExternalCookieAuthenticationScheme);

            // retrieve return URL
            var returnUrl = result.Properties.Items["return_url"] ?? "~/";

            var context = await _interaction.GetAuthorizationContextAsync(returnUrl);
            if (context != null)
            {
                if (await IsPkceClientAsync(context.Client.ClientId))
                {
                    // if the client is PKCE then we assume it's native, so this change in how to
                    // return the response is for better UX for the end user.
                    return View("Redirect", new RedirectViewModel { RedirectUrl = returnUrl });
                }

                // we can trust model.ReturnUrl since GetAuthorizationContextAsync returned non-null
                return Redirect(returnUrl);
            }

            // request for a local page
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            else if (string.IsNullOrEmpty(returnUrl))
            {
                return Redirect("~/");
            }
            else
            {
                // user might have clicked on a malicious link - should be logged
                throw new Exception("invalid return URL");
            }
        }

        private async Task<(User user, string provider, string providerUserId, IEnumerable<Claim> claims)>
            FindUserFromExternalProviderAsync(AuthenticateResult result)
        {
            var externalUser = result.Principal;

            // try to determine the unique id of the external user (issued by the provider)
            // the most common claim type for that are the sub claim and the NameIdentifier
            // depending on the external provider, some other claim type might be used
            var userIdClaim = externalUser.FindFirst(JwtClaimTypes.Subject) ??
                              externalUser.FindFirst(ClaimTypes.NameIdentifier) ??
                              throw new Exception("Unknown userid");

            // remove the user id claim so we don't include it as an extra claim if/when we provision the user
            var claims = externalUser.Claims.ToList();
            claims.Remove(userIdClaim);

            var provider = result.Properties.Items["scheme"];
            var providerUserId = userIdClaim.Value;
            var user = await _userRepository.GetByIdAsync(new Guid(providerUserId));

            return (user, provider, providerUserId, claims);
        }

        private void ProcessLoginCallbackForOidc(AuthenticateResult externalResult,
            List<Claim> localClaims, AuthenticationProperties localSignInProps)
        {
            // if the external system sent a session id claim, copy it over
            // so we can use it for single sign-out
            var sid = externalResult.Principal.Claims.FirstOrDefault(x => x.Type == JwtClaimTypes.SessionId);
            if (sid != null)
            {
                localClaims.Add(new Claim(JwtClaimTypes.SessionId, sid.Value));
            }

            // if the external provider issued an id_token, we'll keep it for signout
            var id_token = externalResult.Properties.GetTokenValue("id_token");
            if (id_token != null)
            {
                localSignInProps.StoreTokens(
                    new[] { new AuthenticationToken { Name = "id_token", Value = id_token } });
            }
        }

        public async Task<bool> IsPkceClientAsync(string client_id)
        {
            if (!string.IsNullOrWhiteSpace(client_id))
            {
                var client = await _clientStore.FindEnabledClientByIdAsync(client_id);
                return client?.RequirePkce == true;
            }
            return false;
        }
    }
}
