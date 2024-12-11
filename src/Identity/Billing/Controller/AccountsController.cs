using Bit.Core;
using Bit.Core.Billing.Models.Api.Requests.Accounts;
using Bit.Core.Billing.TrialInitiation.Registration;
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
    IReferenceEventService referenceEventService
) : Microsoft.AspNetCore.Mvc.Controller
{
    [RequireFeature(FeatureFlagKeys.EmailVerification)]
    [HttpPost("trial/send-verification-email")]
    public async Task<IActionResult> PostTrialInitiationSendVerificationEmailAsync(
        [FromBody] TrialSendVerificationEmailRequestModel model
    )
    {
        var token = await sendTrialInitiationEmailForRegistrationCommand.Handle(
            model.Email,
            model.Name,
            model.ReceiveMarketingEmails,
            model.ProductTier,
            model.Products
        );

        var refEvent = new ReferenceEvent
        {
            Type = ReferenceEventType.SignupEmailSubmit,
            ClientId = currentContext.ClientId,
            ClientVersion = currentContext.ClientVersion,
            Source = ReferenceEventSource.Registration,
        };
        await referenceEventService.RaiseEventAsync(refEvent);

        if (token != null)
        {
            return Ok(token);
        }

        return NoContent();
    }
}
