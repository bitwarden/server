using System;
using System.Diagnostics;
using System.Reflection;
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
        
        [HttpGet("~/version")]
        [AllowAnonymous]
        public JsonResult GetVersion()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var fileVersionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);
            return Json(fileVersionInfo.ProductVersion);
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
