using System;
using Microsoft.AspNetCore.Mvc;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Bit.Core;
using Bit.Core.Exceptions;

namespace Bit.Api.Controllers
{
    [Route("push")]
    [Authorize("Push")]
    public class PushController : Controller
    {
        private readonly IPushRegistrationService _pushRegistrationService;
        private readonly CurrentContext _currentContext;

        public PushController(
            IPushRegistrationService pushRegistrationService,
            CurrentContext currentContext)
        {
            _currentContext = currentContext;
            _pushRegistrationService = pushRegistrationService;
        }

        [HttpGet("register")]
        public Object Register()
        {
            if(!_currentContext.InstallationId.HasValue)
            {
                throw new BadRequestException("bad request.");
            }

            return new { Foo = "bar" };
        }
    }
}
