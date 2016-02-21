using System;
using Microsoft.AspNet.Mvc;

namespace Bit.Api.Controllers
{
    [Route("alive")]
    public class AliveController : Controller
    {
        [HttpGet("")]
        public DateTime Get()
        {
            return DateTime.UtcNow;
        }
    }
}
