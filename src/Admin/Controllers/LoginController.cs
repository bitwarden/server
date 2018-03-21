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

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(LoginModel model)
        {
            if(ModelState.IsValid)
            {
                await _signInManager.PasswordlessSignInAsync(model.Email);
                return RedirectToAction("Index", "Home");
            }

            return View(model);
        }

        public async Task<IActionResult> Confirm(string email, string token)
        {
            var result = await _signInManager.PasswordlessSignInAsync(email, token, false);
            if(!result.Succeeded)
            {
                return View("Error");
            }

            return RedirectToAction("Index", "Home");
        }
    }
}
