using Bit.Api.Models.Response;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Controllers;

[Route("plans")]
[Authorize("Web")]
public class PlansController : Controller
{
    [HttpGet("")]
    [AllowAnonymous]
    public ListResponseModel<PlanResponseModel> Get()
    {
        var responses = StaticStore.Plans.Select(plan => new PlanResponseModel(plan));
        return new ListResponseModel<PlanResponseModel>(responses);
    }
}
