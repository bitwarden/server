// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.Security.Claims;
using Bit.Admin.Auth.IdentityServer;
using Bit.Admin.Auth.Models;
using Bit.Admin.IdentityServer;
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

    public LoginController(
        PasswordlessSignInManager<IdentityUser> signInManager,
        UserManager<IdentityUser> userManager,
        IOptions<AdminSettings> adminSettings)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _adminSettings = adminSettings.Value;
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
            SsoEnabled = _adminSettings.OidcEnabled,
            SsoDisplayName = _adminSettings.Oidc?.DisplayName
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
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

        model.SsoEnabled = _adminSettings.OidcEnabled;
        model.SsoDisplayName = _adminSettings.Oidc?.DisplayName;
        return View(model);
    }

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
            return RedirectToAction("Index", new { error = 5 });
        }

        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info == null)
        {
            return RedirectToAction("Index", new { error = 5 });
        }

        var email = info.Principal.FindFirst(_adminSettings.Oidc.EmailClaimType)?.Value;
        if (string.IsNullOrWhiteSpace(email))
        {
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
            return RedirectToAction("Index", new { error = 5 });
        }

        var normalizedEmail = email.ToLowerInvariant();
        var user = await _userManager.FindByEmailAsync(normalizedEmail);
        if (user == null)
        {
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
            return RedirectToAction("Index", new { error = 4 });
        }

        var ssoMarker = new[]
        {
            new Claim(AdminAuthenticationSchemes.AuthMethodClaimType, AdminAuthenticationSchemes.AuthMethodSso)
        };
        await _signInManager.SignInWithClaimsAsync(user, isPersistent: false, ssoMarker);
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

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

        if (signedInViaSso)
        {
            var loggedOutRedirect = Url.Action(nameof(Index), "Login", new { success = 1 });
            return SignOut(
                new AuthenticationProperties { RedirectUri = loggedOutRedirect },
                AdminAuthenticationSchemes.UpstreamOidc);
        }

        return RedirectToAction("Index", new
        {
            success = 1
        });
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
