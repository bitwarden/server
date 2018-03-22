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

        public IActionResult Index(string returnUrl = null)
        {
            return View(new LoginModel
            {
                ReturnUrl = returnUrl
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(LoginModel model)
        {
            if(ModelState.IsValid)
            {
                await _signInManager.PasswordlessSignInAsync(model.Email,
                    Url.Action("Confirm", "Login", new { returnUrl = model.ReturnUrl }, Request.Scheme));
                return RedirectToAction("Index", "Home");
            }

            return View(model);
        }

        public async Task<IActionResult> Confirm(string email, string token, string returnUrl)
        {
            var result = await _signInManager.PasswordlessSignInAsync(email, token, false);
            if(!result.Succeeded)
            {
                // TODO: error?
                return RedirectToAction("Index");
            }

            if(!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Home");
        }
    }
}
