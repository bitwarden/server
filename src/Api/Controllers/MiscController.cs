using System;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using Bit.Core.Models.Api;

namespace Bit.Api.Controllers
{
    public class MiscController : Controller
    {
        [HttpGet("~/alive")]
        public DateTime Get()
        {
            return DateTime.UtcNow;
        }

        [HttpGet("~/claims")]
        public IActionResult Claims()
        {
            return new JsonResult(User?.Claims?.Select(c => new { c.Type, c.Value }));
        }

        [HttpGet("~/version")]
        public VersionResponseModel Version()
        {
            return new VersionResponseModel();
        }
    }
}
