// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.Security.Claims;
using Bit.Admin.Auth.Filters;
using Bit.Admin.Auth.IdentityServer;
using Bit.Admin.Auth.Models;
using Bit.Admin.Auth.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Bit.Admin.Auth.Controllers;

public class LoginController : Controller
{
    private readonly PasswordlessSignInManager<IdentityUser> _signInManager;
    private readonly AdminSettings _adminSettings;
    private readonly ISsoSignInPrincipalBuilder _ssoSignInPrincipalBuilder;
    private readonly ILogger<LoginController> _logger;

    public LoginController(
        PasswordlessSignInManager<IdentityUser> signInManager,
        IOptions<AdminSettings> adminSettings,
        ISsoSignInPrincipalBuilder ssoSignInPrincipalBuilder,
        ILogger<LoginController> logger)
    {
        _signInManager = signInManager;
        _adminSettings = adminSettings.Value;
        _ssoSignInPrincipalBuilder = ssoSignInPrincipalBuilder;
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
    [RequirePasswordlessLogin]
    public async Task<IActionResult> Index(LoginModel model)
    {
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

    [RequirePasswordlessLogin]
    public async Task<IActionResult> Confirm(string email, string token, string returnUrl)
    {
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
    [RequireOidc]
    public IActionResult Sso(string returnUrl = null)
    {
        var redirectUrl = Url.Action(nameof(SsoSignIn), "Login", new { returnUrl });
        var properties = _signInManager.ConfigureExternalAuthenticationProperties(
            AdminAuthenticationSchemes.UpstreamOidc, redirectUrl);
        return Challenge(properties, AdminAuthenticationSchemes.UpstreamOidc);
    }

    [HttpGet("login/sso-signin")]
    [AllowAnonymous]
    [RequireOidc]
    public async Task<IActionResult> SsoSignIn(string returnUrl = null, string remoteError = null)
    {
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

        // Set IsPersistent = false, so SSO sessions die when the browser closes.
        // The passwordless flow uses IsPersistent = true and survives a browser restart.
        var props = new AuthenticationProperties { IsPersistent = false };

        // Store id_token on the cookie. Two purposes:
        //   1. It's the value we'll attach as id_token_hint on RP-initiated logout so the
        //      upstream IdP can identify the client and honor the app-level Allowed Logout
        //      URLs list.
        //   2. Its presence at logout time is what tells Logout() this was an SSO session -
        //      the passwordless flow never stores tokens, so no separate marker is needed.
        // Only id_token is stored; access/refresh tokens are unused by the Admin app and
        // would be unnecessary attack surface if the cookie were ever exfiltrated.
        var idToken = external.Properties?.GetTokenValue("id_token");
        if (!string.IsNullOrEmpty(idToken))
        {
            props.StoreTokens([new AuthenticationToken { Name = "id_token", Value = idToken }]);
        }

        // SSO signs in to its own dedicated cookie scheme with a hand-built principal.
        // There's no local user record to look up and no SecurityStamp to track - the IdP owns
        // identity, and session lifetime  is bounded by the cookie expiry. 
        // A separate scheme (not the Identity application cookie) is required because 
        // Identity's SecurityStampValidator enforces security stamp consistency for the
        // local user store.
        var principal = _ssoSignInPrincipalBuilder.Build(email, external.Principal);

        // Tenancy gates. Each configured signal must admit the user. See src/Admin/Auth/SSO.md.
        var allowlistConfigured = !string.IsNullOrWhiteSpace(_adminSettings.Admins);
        var groupMappingConfigured = !string.IsNullOrWhiteSpace(_adminSettings.Oidc.GroupPrefix);

        if (allowlistConfigured && !IsOnAdminAllowlist(email))
        {
            _logger.LogWarning(
                "SSO sign-in rejected: email not on AdminSettings:Admins allowlist.");
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
            return RedirectToAction("Index", new { error = 5 });
        }

        if (groupMappingConfigured && !principal.FindAll(ClaimTypes.Role).Any())
        {
            _logger.LogWarning(
                "SSO sign-in rejected: authenticated principal has no mapped admin role.");
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
            return RedirectToAction("Index", new { error = 5 });
        }

        await HttpContext.SignInAsync(AdminAuthenticationSchemes.SsoCookie, principal, props);
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
        // Presence of `id_token` on the SSO cookie is both (a) the marker that this is an SSO
        // session (the passwordless flow writes to a different cookie and stores no tokens)
        // and (b) the value we need as id_token_hint on RP-initiated logout. Read before the
        // sign-outs below clear the cookie.
        var idToken = await HttpContext.GetTokenAsync(AdminAuthenticationSchemes.SsoCookie, "id_token");

        // Sign out of both cookie schemes. SignOutAsync on a scheme the user isn't currently
        // signed in to is a no-op, so this covers both passwordless and SSO sessions cleanly.
        await _signInManager.SignOutAsync();
        await HttpContext.SignOutAsync(AdminAuthenticationSchemes.SsoCookie);

        var loggedOutRedirect = Url.Action(nameof(Index), "Login", new { success = 1 });

        // If OIDC was removed from config while an SSO-signed-in
        // admin still holds a valid cookie, SignOut against the unregistered scheme would
        // throw InvalidOperationException (500).
        if (!string.IsNullOrEmpty(idToken) && _adminSettings.OidcEnabled)
        {
            var props = new AuthenticationProperties { RedirectUri = loggedOutRedirect };
            props.StoreTokens([new AuthenticationToken { Name = "id_token", Value = idToken }]);
            return SignOut(props, AdminAuthenticationSchemes.UpstreamOidc);
        }

        return Redirect(loggedOutRedirect);
    }

    // AdminSettings:Admins is a comma-separated list where each entry is either "email" or
    // "email:stamp" (the optional stamp is used by the passwordless path's user store).
    // Match the email prefix of each entry, case-insensitive.
    private bool IsOnAdminAllowlist(string email)
    {
        var csv = _adminSettings.Admins;
        if (string.IsNullOrWhiteSpace(csv))
        {
            return false;
        }
        var normalized = email.Trim().ToLowerInvariant();
        return csv.ToLowerInvariant()
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(entry => entry.Split(':', 2)[0].Trim())
            .Any(e => e == normalized);
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
