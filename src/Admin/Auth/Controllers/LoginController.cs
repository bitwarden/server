// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Admin.Auth.IdentityServer;
using Bit.Admin.Auth.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Bit.Admin.Auth.Controllers;

public class LoginController : Controller
{
    private readonly PasswordlessSignInManager<IdentityUser> _signInManager;
    private readonly ILogger<LoginController> _logger;

    public LoginController(
        PasswordlessSignInManager<IdentityUser> signInManager,
        ILogger<LoginController> logger)
    {
        _signInManager = signInManager;
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
            Success = GetMessage(success)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(LoginModel model)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await _signInManager.PasswordlessSignInAsync(model.Email, model.ReturnUrl);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error sending login email");
                return RedirectToAction("Index", new { error = 5 });
            }

            return RedirectToAction("Index", new
            {
                success = 3
            });
        }

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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
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
            5 => "There was a problem sending the login email. Please check your mail server configuration.",
            _ => null,
        };
    }
}
