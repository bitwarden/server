using Microsoft.AspNetCore.Mvc;

namespace Billing.Controllers;

public class LoginController : Controller
{
    /*
    private readonly PasswordlessSignInManager<IdentityUser> _signInManager;

    public LoginController(
        PasswordlessSignInManager<IdentityUser> signInManager)
    {
        _signInManager = signInManager;
    }

    public IActionResult Index()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(LoginModel model)
    {
        if (ModelState.IsValid)
        {
            var result = await _signInManager.PasswordlessSignInAsync(model.Email,
                Url.Action("Confirm", "Login", null, Request.Scheme));
            if (result.Succeeded)
            {
                return RedirectToAction("Index", "Home");
            }
            else
            {
                ModelState.AddModelError(string.Empty, "Account not found.");
            }
        }

        return View(model);
    }

    public async Task<IActionResult> Confirm(string email, string token)
    {
        var result = await _signInManager.PasswordlessSignInAsync(email, token, false);
        if (!result.Succeeded)
        {
            return View("Error");
        }

        return RedirectToAction("Index", "Home");
    }
    */
}
