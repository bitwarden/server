using System;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Admin.Controllers
{
    public class MiscController : Controller
    {
        public MiscController() { }

        [HttpGet("~/alive")]
        [HttpGet("~/now")]
        public DateTime Get()
        {
            return DateTime.UtcNow;
        }
    }
}
