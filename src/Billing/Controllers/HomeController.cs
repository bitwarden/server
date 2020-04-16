using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Billing.Controllers
{
    public class HomeController : Controller
    {
        [HttpGet("~/alive")]
        [HttpGet("~/now")]
        [AllowAnonymous]
        public DateTime GetAlive()
        {
            return DateTime.UtcNow;
        }

        /*
        [Authorize]
        public IActionResult Index()
        {
            return View();
        }
        */
    }
}
