using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Bit.Core.Utilities;
using Bit.Core.Models.Api;
using System.Linq;

namespace Bit.Api.Controllers
{
    [Route("plans")]
    [Authorize("Web")]
    public class PlansController : Controller
    {
        [HttpGet("")]
        public ListResponseModel<PlanResponseModel> Get()
        {
            var data = StaticStore.Plans;
            var responses = data.Select(plan => new PlanResponseModel(plan));
            return new ListResponseModel<PlanResponseModel>(responses);
        }
    }
}
