using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Bit.Core.Utilities;
using Bit.Core.Models.StaticStore;

namespace Bit.Api.Controllers
{
    [Route("plans")]
    [Authorize("Web")]
    public class PlanSController : Controller
    {
        [HttpGet("")]
        public IEnumerable<Plan> Get()
        {
            return StaticStore.Plans;
        }
    }
}
