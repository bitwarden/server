using System.Threading.Tasks;
using Bit.Portal.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Portal.Controllers
{
    public class AuthController : Controller
    {
        private readonly EnterprisePortalTokenSignInManager _signInManager;

        public AuthController(
            EnterprisePortalTokenSignInManager signInManager)
        {
            _signInManager = signInManager;
        }

        [HttpGet("~/login")]
        public async Task<IActionResult> Index(string userId, string token, string organizationId, string returnUrl)
        {
            var result = await _signInManager.TokenSignInAsync(userId, token, false);
            if (!result.Succeeded)
            {
                return RedirectToAction("Index", "Home", new
                {
                    error = 2
                });
            }

            if (!string.IsNullOrWhiteSpace(organizationId))
            {
                Response.Cookies.Append("SelectedOrganization", organizationId, new CookieOptions { HttpOnly = true });
            }

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Home");
        }

        [HttpPost("~/logout")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("LoggedOut");
        }

        [HttpGet("~/logged-out")]
        public IActionResult LoggedOut()
        {
            return View();
        }

        [HttpGet("~/access-denied")]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
