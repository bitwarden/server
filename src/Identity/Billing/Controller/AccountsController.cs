using Bit.Core.Billing.Trials.Commands;
using Bit.Core.Billing.Trials.Requests;
using Bit.Core.Context;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Business;
using Bit.Core.Tools.Services;
using Bit.Core.Utilities;
using Bit.SharedWeb.Utilities;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Identity.Billing.Controller;

[Route("accounts")]
[ExceptionHandlerFilter]
public class AccountsController(
    ICurrentContext currentContext,
    ISendTrialInitiationEmailForRegistrationCommand sendTrialInitiationEmailForRegistrationCommand,
    IReferenceEventService referenceEventService) : Microsoft.AspNetCore.Mvc.Controller
{
    [HttpPost("trial/send-verification-email")]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task<IActionResult> PostTrialInitiationSendVerificationEmailAsync([FromBody] TrialSendVerificationEmailRequestModel model)
    {
        var token = await sendTrialInitiationEmailForRegistrationCommand.Handle(
            model.Email,
            model.Name,
            model.ReceiveMarketingEmails,
            model.ProductTier,
            model.Products);

        var refEvent = new ReferenceEvent
        {
            Type = ReferenceEventType.SignupEmailSubmit,
            ClientId = currentContext.ClientId,
            ClientVersion = currentContext.ClientVersion,
            Source = ReferenceEventSource.Registration
        };
        await referenceEventService.RaiseEventAsync(refEvent);

        if (token != null)
        {
            return Ok(token);
        }

        return NoContent();
    }
}
