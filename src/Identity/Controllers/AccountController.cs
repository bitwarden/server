using Bit.Core.Models.Table;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Identity.Models;
using IdentityModel;
using IdentityServer4;
using IdentityServer4.Services;
using IdentityServer4.Stores;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Bit.Identity.Controllers
{
    public class AccountController : Controller
    {
        private readonly IClientStore _clientStore;
        private readonly IIdentityServerInteractionService _interaction;
        private readonly ILogger<AccountController> _logger;
        private readonly ISsoConfigRepository _ssoConfigRepository;
        private readonly IUserRepository _userRepository;

        public AccountController(
            IClientStore clientStore,
            IIdentityServerInteractionService interaction,
            ILogger<AccountController> logger,
            IOrganizationUserRepository organizationUserRepository,
            ISsoConfigRepository ssoConfigRepository,
            IUserRepository userRepository,
            IUserService userService)
        {
            _clientStore = clientStore;
            _interaction = interaction;
            _logger = logger;
            _ssoConfigRepository = ssoConfigRepository;
            _userRepository = userRepository;
        }

        [HttpGet]
        public async Task<IActionResult> Login(string returnUrl)
        {
            var context = await _interaction.GetAuthorizationContextAsync(returnUrl);

            var domainHint = context.Parameters.AllKeys.Contains("domain_hint") ? 
                context.Parameters["domain_hint"] : null;

            if (string.IsNullOrWhiteSpace(domainHint))
            {
                throw new Exception("No domain_hint provided");
            }

            var userIdentifier = context.Parameters.AllKeys.Contains("user_identifier") ? 
                context.Parameters["user_identifier"] : null;

            return RedirectToAction(nameof(ExternalChallenge), new
            {
                organizationIdentifier = domainHint,
                returnUrl,
                userIdentifier
            });
        }

        [HttpGet]
        public async Task<IActionResult> ExternalChallenge(string organizationIdentifier, string returnUrl,
            string userIdentifier)
        {
            if (string.IsNullOrWhiteSpace(organizationIdentifier))
            {
                throw new Exception("Invalid organization reference id.");
            }

            var ssoConfig = await _ssoConfigRepository.GetByIdentifierAsync(organizationIdentifier);
            if (ssoConfig == null || !ssoConfig.Enabled)
            {
                throw new Exception("Organization not found or SSO configuration not enabled");
            }
            var domainHint = ssoConfig.OrganizationId.ToString();

            var scheme = "sso";
            var props = new AuthenticationProperties
            {
                RedirectUri = Url.Action(nameof(ExternalCallback)),
                Items =
                {
                    { "return_url", returnUrl },
                    { "domain_hint", domainHint },
                    { "scheme", scheme },
                },
            };

            if (!string.IsNullOrWhiteSpace(userIdentifier))
            {
                props.Items.Add("user_identifier", userIdentifier);
            }

            return Challenge(props, scheme);
        }

        [HttpGet]
        public async Task<ActionResult> ExternalCallback()
        {
            // Read external identity from the temporary cookie
            var result = await HttpContext.AuthenticateAsync(
                IdentityServerConstants.ExternalCookieAuthenticationScheme);
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

            // This allows us to collect any additional claims or properties
            // for the specific protocols used and store them in the local auth cookie.
            // this is typically used to store data needed for signout from those protocols.
            var additionalLocalClaims = new List<Claim>();
            var localSignInProps = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(1)
            };
            ProcessLoginCallback(result, additionalLocalClaims, localSignInProps);

            // Issue authentication cookie for user
            await HttpContext.SignInAsync(new IdentityServerUser(user.Id.ToString())
            {
                DisplayName = user.Email,
                IdentityProvider = provider,
                AdditionalClaims = additionalLocalClaims.ToArray()
            }, localSignInProps);

            // Delete temporary cookie used during external authentication
            await HttpContext.SignOutAsync(IdentityServerConstants.ExternalCookieAuthenticationScheme);

            // Retrieve return URL
            var returnUrl = result.Properties.Items["return_url"] ?? "~/";

            var context = await _interaction.GetAuthorizationContextAsync(returnUrl);
            if (context != null)
            {
                if (IsNativeClient(context))
                {
                    // The client is native, so this change in how to
                    // return the response is for better UX for the end user.
                    HttpContext.Response.StatusCode = 200;
                    HttpContext.Response.Headers["Location"] = string.Empty;
                    return View("Redirect", new RedirectViewModel { RedirectUrl = returnUrl });
                }

                // We can trust model.ReturnUrl since GetAuthorizationContextAsync returned non-null
                return Redirect(returnUrl);
            }

            // Request for a local page
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
                // User might have clicked on a malicious link - should be logged
                throw new Exception("invalid return URL");
            }
        }

        private async Task<(User user, string provider, string providerUserId, IEnumerable<Claim> claims)>
            FindUserFromExternalProviderAsync(AuthenticateResult result)
        {
            var externalUser = result.Principal;

            // Try to determine the unique id of the external user (issued by the provider)
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

        private void ProcessLoginCallback(AuthenticateResult externalResult, List<Claim> localClaims,
            AuthenticationProperties localSignInProps)
        {
            // If the external system sent a session id claim, copy it over
            // so we can use it for single sign-out
            var sid = externalResult.Principal.Claims.FirstOrDefault(x => x.Type == JwtClaimTypes.SessionId);
            if (sid != null)
            {
                localClaims.Add(new Claim(JwtClaimTypes.SessionId, sid.Value));
            }

            // If the external provider issued an idToken, we'll keep it for signout
            var idToken = externalResult.Properties.GetTokenValue("id_token");
            if (idToken != null)
            {
                localSignInProps.StoreTokens(
                    new[] { new AuthenticationToken { Name = "id_token", Value = idToken } });
            }
        }

        public bool IsNativeClient(IdentityServer4.Models.AuthorizationRequest context)
        {
            return !context.RedirectUri.StartsWith("https", StringComparison.Ordinal)
               && !context.RedirectUri.StartsWith("http", StringComparison.Ordinal);
        }
    }
}
