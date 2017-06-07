using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Bit.Core.Exceptions;

namespace Bit.Api.Controllers
{
    [Obsolete]
    [Route("auth")]
    public class AuthController : Controller
    {
        [HttpPost("token")]
        [AllowAnonymous]
        public IActionResult PostToken()
        {
            throw new BadRequestException("You are using an outdated version of bitwarden that is no longer supported. " +
                "Please update your app first and try again.");
        }
    }
}
