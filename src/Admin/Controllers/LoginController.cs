using System.Threading.Tasks;
using Bit.Admin.Models;
using Bit.Core.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Admin.Controllers
{
    public class LoginController : Controller
    {
        private readonly PasswordlessSignInManager<IdentityUser> _signInManager;

        public LoginController(
            PasswordlessSignInManager<IdentityUser> signInManager)
        {
            _signInManager = signInManager;
        }

        public IActionResult Index(string returnUrl = null, string error = null, string success = null,
            bool accessDenied = false)
        {
            if (string.IsNullOrWhiteSpace(error) && accessDenied)
            {
                error = "Access denied. Please log in.";
            }

            return View(new LoginModel
            {
                ReturnUrl = returnUrl,
                Error = error,
                Success = success
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
                    success = "If a valid admin user with this email address exists, " +
                        "we've sent you an email with a secure link to log in."
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
                    error = "This login confirmation link is invalid. Try logging in again."
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
                success = "You have been logged out."
            });
        }
    }
}
