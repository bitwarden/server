using Bit.Core;
using Bit.Core.Billing.Models.Api.Requests.Accounts;
using Bit.Core.Billing.TrialInitiation.Registration;
using Bit.Core.Context;
using Bit.Core.Services;
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
    IReferenceEventService referenceEventService,
    IFeatureService featureService) : Microsoft.AspNetCore.Mvc.Controller
{
    [HttpPost("trial/send-verification-email")]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task<IActionResult> PostTrialInitiationSendVerificationEmailAsync([FromBody] TrialSendVerificationEmailRequestModel model)
    {
        var allowTrialLength0 = featureService.IsEnabled(FeatureFlagKeys.PM20322_AllowTrialLength0);

        var trialLength = allowTrialLength0 ? model.TrialLength ?? 7 : 7;

        var token = await sendTrialInitiationEmailForRegistrationCommand.Handle(
            model.Email,
            model.Name,
            model.ReceiveMarketingEmails,
            model.ProductTier,
            model.Products,
            trialLength);

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
