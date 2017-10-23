using System;
using Microsoft.AspNetCore.Mvc;
using Bit.Core.Models.Api;

namespace Bit.Api.Controllers
{
    public class MiscController : Controller
    {
        [HttpGet("~/alive")]
        [HttpGet("~/now")]
        public DateTime Get()
        {
            return DateTime.UtcNow;
        }

        [HttpGet("~/version")]
        public VersionResponseModel Version()
        {
            return new VersionResponseModel();
        }
    }
}
