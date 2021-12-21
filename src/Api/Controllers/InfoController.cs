using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Bit.Core.Utilities;

namespace Bit.Api.Controllers
{
    public class InfoController : Controller
    {
        [HttpGet("~/alive")]
        [HttpGet("~/now")]
        public DateTime GetAlive()
        {
            return DateTime.UtcNow;
        }

        [HttpGet("~/version")]
        public JsonResult GetVersion()
        {
            return Json(CoreHelpers.GetVersion());
        }
        
        [HttpGet("~/ip")]
        public JsonResult Ip()
        {
            var headerSet = new HashSet<string> { "x-forwarded-for", "cf-connecting-ip", "client-ip" };
            var headers = HttpContext.Request?.Headers
                .Where(h => headerSet.Contains(h.Key.ToLower()))
                .ToDictionary(h => h.Key);
            return new JsonResult(new
            {
                Ip = HttpContext.Connection?.RemoteIpAddress?.ToString(),
                Headers = headers,
            });
        }
    }
}
