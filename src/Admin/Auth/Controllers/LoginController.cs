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
            PasswordlessLoginEnabled = _adminSettings.PasswordlessLoginEnabled,
            SsoEnabled = _adminSettings.OidcEnabled,
            SsoDisplayName = _adminSettings.Oidc?.DisplayName
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(LoginModel model)
    {
        if (!_adminSettings.PasswordlessLoginEnabled)
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

        model.PasswordlessLoginEnabled = _adminSettings.PasswordlessLoginEnabled;
        model.SsoEnabled = _adminSettings.OidcEnabled;
        model.SsoDisplayName = _adminSettings.Oidc?.DisplayName;
        return View(model);
    }

    public async Task<IActionResult> Confirm(string email, string token, string returnUrl)
    {
        if (!_adminSettings.PasswordlessLoginEnabled)
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

        if (!IsEmailVerified(external.Principal))
        {
            _logger.LogWarning("SSO sign-in rejected: email_verified claim is not true.");
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
            return RedirectToAction("Index", new { error = 5 });
        }

        if (!IsAuthTimeWithinMaxAge(external.Principal))
        {
            _logger.LogWarning("SSO sign-in rejected: IdP auth_time missing or exceeds max_age.");
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

        var ssoMarker = new[]
        {
            new Claim(AdminAuthenticationSchemes.AuthMethodClaimType, AdminAuthenticationSchemes.AuthMethodSso)
        };
        var props = new AuthenticationProperties { IsPersistent = false };
        // Store only id_token (needed for id_token_hint on RP-initiated logout). Access/refresh
        // tokens go unused by this app - keeping them in the cookie is unnecessary attack surface
        // if the cookie is ever exfiltrated.
        var idToken = external.Properties?.GetTokenValue("id_token");
        if (!string.IsNullOrEmpty(idToken))
        {
            props.StoreTokens([new AuthenticationToken { Name = "id_token", Value = idToken }]);
        }
        await _signInManager.SignInWithClaimsAsync(user, props, ssoMarker);
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
        var signedInViaSso = User.HasClaim(
            AdminAuthenticationSchemes.AuthMethodClaimType, AdminAuthenticationSchemes.AuthMethodSso);

        await _signInManager.SignOutAsync();

        var loggedOutRedirect = Url.Action(nameof(Index), "Login", new { success = 1 });

        if (signedInViaSso)
        {
            return SignOut(
                new AuthenticationProperties { RedirectUri = loggedOutRedirect },
                AdminAuthenticationSchemes.UpstreamOidc);
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

    private static bool IsAuthTimeWithinMaxAge(ClaimsPrincipal principal)
    {
        // The OIDC handler does not validate `auth_time` against the `max_age` we sent, so a
        // non-conformant IdP could hand us a stale session. Enforce here. Fail-secure: reject
        // if the claim is missing or unparseable (the spec REQUIRES the IdP to return
        // auth_time whenever max_age is present in the request).
        var claim = principal.FindFirst("auth_time")?.Value;
        if (!long.TryParse(claim, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var authTime))
        {
            return false;
        }
        var age = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - authTime;
        return age >= 0 && age <= AdminAuthenticationSchemes.MaxIdpAuthAgeSeconds;
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
