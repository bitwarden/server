using Bit.Admin.Billing.Models.ProcessStripeEvents;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Admin.Billing.Controllers;

[Authorize]
[Route("process-stripe-events")]
[SelfHosted(NotSelfHostedOnly = true)]
public class ProcessStripeEventsController : Controller
{
    [HttpGet]
    public ActionResult Index()
    {
        return View(new EventsFormModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ProcessAsync([FromForm] EventsFormModel model)
    {
        throw new NotImplementedException();
    }
}
