using System;
using Microsoft.AspNetCore.Mvc;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Api.Controllers
{
    [Route("push")]
    [Authorize("Push")]
    public class PushController : Controller
    {
        private readonly IPushRegistrationService _pushRegistrationService;

        public PushController(
            IPushRegistrationService pushRegistrationService)
        {
            _pushRegistrationService = pushRegistrationService;
        }

        [HttpGet("register")]
        public Object Register()
        {
            return new { Foo = "bar" };
        }
    }
}
