// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.Security.Claims;
using Bit.Admin.Auth.IdentityServer;
using Bit.Admin.Auth.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Bit.Admin.Auth.Controllers;

public class LoginController : Controller
{
    private readonly PasswordlessSignInManager<IdentityUser> _signInManager;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly AdminSettings _adminSettings;
    private readonly ILogger<LoginController> _logger;

    public LoginController(
        PasswordlessSignInManager<IdentityUser> signInManager,
        UserManager<IdentityUser> userManager,
        IOptions<AdminSettings> adminSettings,
        ILogger<LoginController> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _adminSettings = adminSettings.Value;
        _logger = logger;
    }

    public IActionResult Index(string returnUrl = null, int? error = null, int? success = null,
        bool accessDenied = false)
    {
        if (!error.HasValue && accessDenied)
        {
            error = 4;
        }

        return View(new LoginModel
        {
            ReturnUrl = returnUrl,
            Error = GetMessage(error),
            Success = GetMessage(success),
            EnablePasswordlessLogin = _adminSettings.EnablePasswordlessLogin,
            SsoEnabled = _adminSettings.OidcEnabled,
            SsoDisplayName = _adminSettings.Oidc?.DisplayName
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(LoginModel model)
    {
        if (!_adminSettings.EnablePasswordlessLogin)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            await _signInManager.PasswordlessSignInAsync(model.Email, model.ReturnUrl);
            return RedirectToAction("Index", new
            {
                success = 3
            });
        }

        model.EnablePasswordlessLogin = _adminSettings.EnablePasswordlessLogin;
        model.SsoEnabled = _adminSettings.OidcEnabled;
        model.SsoDisplayName = _adminSettings.Oidc?.DisplayName;
        return View(model);
    }

    public async Task<IActionResult> Confirm(string email, string token, string returnUrl)
    {
        if (!_adminSettings.EnablePasswordlessLogin)
        {
            return NotFound();
        }

        var result = await _signInManager.PasswordlessSignInAsync(email, token, true);
        if (!result.Succeeded)
        {
            return RedirectToAction("Index", new
            {
                error = 2
            });
        }

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Index", "Home");
    }

    [HttpGet("login/sso")]
    [AllowAnonymous]
    public IActionResult Sso(string returnUrl = null)
    {
        if (!_adminSettings.OidcEnabled)
        {
            return NotFound();
        }

        var redirectUrl = Url.Action(nameof(SsoSignIn), "Login", new { returnUrl });
        var properties = _signInManager.ConfigureExternalAuthenticationProperties(
            AdminAuthenticationSchemes.UpstreamOidc, redirectUrl);
        return Challenge(properties, AdminAuthenticationSchemes.UpstreamOidc);
    }

    [HttpGet("login/sso-signin")]
    [AllowAnonymous]
    public async Task<IActionResult> SsoSignIn(string returnUrl = null, string remoteError = null)
    {
        if (!_adminSettings.OidcEnabled)
        {
            return NotFound();
        }

        if (!string.IsNullOrEmpty(remoteError))
        {
            _logger.LogWarning("SSO sign-in rejected: upstream IdP returned remote error.");
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
            return RedirectToAction("Index", new { error = 5 });
        }

        var external = await HttpContext.AuthenticateAsync(IdentityConstants.ExternalScheme);
        if (!external.Succeeded)
        {
            _logger.LogWarning("SSO sign-in rejected: no external principal from upstream IdP.");
            return RedirectToAction("Index", new { error = 5 });
        }

        var email = external.Principal.FindFirst(_adminSettings.Oidc.EmailClaimType)?.Value;
        if (string.IsNullOrWhiteSpace(email))
        {
            _logger.LogWarning("SSO sign-in rejected: email claim ({ClaimType}) missing from upstream principal.",
                _adminSettings.Oidc.EmailClaimType);
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
            return RedirectToAction("Index", new { error = 5 });
        }

        // Require the IdP to have verified the email address before we treat it as the
        // identity to match against the admin allowlist. Without this check, any IdP that
        // permits self-registration (public Auth0/Okta tenants, Keycloak realms with sign-up
        // enabled, etc.) would let an attacker create an account claiming an admin's email
        // and get in. The email address is the entire authorization key here, so we must be
        // sure that it's been verified.
        if (!IsEmailVerified(external.Principal))
        {
            _logger.LogWarning("SSO sign-in rejected: email_verified claim is not true.");
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
            return RedirectToAction("Index", new { error = 5 });
        }

        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            // Same error code as other reject branches. Distinct codes would let any authenticated
            // IdP user enumerate which emails are on the admin allowlist.
            _logger.LogWarning("SSO sign-in rejected: principal not on admin allowlist.");
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
            return RedirectToAction("Index", new { error = 5 });
        }

        // IsPersistent = false: SSO sessions die when the browser closes, by design. The
        // passwordless flow uses IsPersistent = true and survives a browser restart; SSO
        // deliberately doesn't, so a shared/kiosk browser doesn't leave an admin session
        // reachable after the operator walks away. Combined with prompt=login on every
        // sign-in, this makes each new browser session pay the full IdP re-auth cost.
        var props = new AuthenticationProperties { IsPersistent = false };
        // Store id_token on the cookie. Two purposes:
        //   1. It's the value we'll attach as id_token_hint on RP-initiated logout so the
        //      upstream IdP can identify the client and honor the app-level Allowed Logout
        //      URLs list (without it, Auth0/Okta fall back to tenant-level URLs).
        //   2. Its presence at logout time is what tells Logout() this was an SSO session -
        //      the passwordless flow never stores tokens, so no separate marker is needed.
        // Only id_token is stored; access/refresh tokens are unused by the Admin app and
        // would be unnecessary attack surface if the cookie were ever exfiltrated.
        var idToken = external.Properties?.GetTokenValue("id_token");
        if (!string.IsNullOrEmpty(idToken))
        {
            props.StoreTokens([new AuthenticationToken { Name = "id_token", Value = idToken }]);
        }

        // Direct SignInWithClaimsAsync bypasses PreSignInCheck/CanSignInAsync that the
        // passwordless path goes through via PasswordlessSignInAsync. Deliberate: impact is
        // nil against ReadOnlyEnvIdentityUserStore (no lockout, always confirmed), and the
        // OIDC handler + email_verified + allowlist checks above are the authorization gate
        // for the SSO path.
        await _signInManager.SignInWithClaimsAsync(user, props, Array.Empty<Claim>());
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
        _logger.LogInformation("SSO sign-in succeeded.");

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        // Presence of `id_token` on the app cookie means this is an SSO session (the
        // passwordless flow never stores tokens), so it's both the "was SSO" signal and the
        // value we need for id_token_hint. Read before SignOutAsync so we don't rely on
        // handler-level caching to serve it back afterward.
        var auth = await HttpContext.AuthenticateAsync();
        var idToken = auth.Properties?.GetTokenValue("id_token");

        await _signInManager.SignOutAsync();

        var loggedOutRedirect = Url.Action(nameof(Index), "Login", new { success = 1 });

        // Also gate on OidcEnabled: if OIDC was removed from config while an SSO-signed-in
        // admin still holds a valid cookie, SignOut against the unregistered scheme would
        // throw InvalidOperationException (500) on the highest-privilege surface.
        if (!string.IsNullOrEmpty(idToken) && _adminSettings.OidcEnabled)
        {
            var props = new AuthenticationProperties { RedirectUri = loggedOutRedirect };
            props.StoreTokens([new AuthenticationToken { Name = "id_token", Value = idToken }]);
            return SignOut(props, AdminAuthenticationSchemes.UpstreamOidc);
        }

        return Redirect(loggedOutRedirect);
    }

    private bool IsEmailVerified(ClaimsPrincipal principal)
    {
        var claim = principal.FindFirst("email_verified")?.Value;
        if (string.IsNullOrEmpty(claim))
        {
            return !_adminSettings.Oidc.RequireEmailVerifiedClaim;
        }
        return bool.TryParse(claim, out var verified) && verified;
    }

    private string GetMessage(int? messageCode)
    {
        return messageCode switch
        {
            1 => "You have been logged out.",
            2 => "This login confirmation link is invalid. Try logging in again.",
            3 => "If a valid admin user with this email address exists, " +
                "we've sent you an email with a secure link to log in.",
            4 => "Access denied. Please log in.",
            5 => "SSO sign-in failed. Try again or use the email link.",
            _ => null,
        };
    }
}
