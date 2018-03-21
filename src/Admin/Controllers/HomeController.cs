using System;
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Bit.Admin.Models;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Admin.Controllers
{
    public class HomeController : Controller
    {
        [Authorize]
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }
    }
}
