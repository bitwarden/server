using Bit.Core;
using Bit.Core.Billing.Models.Api.Requests.Accounts;
using Bit.Core.Billing.TrialInitiation.Registration;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Bit.SharedWeb.Utilities;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Identity.Billing.Controller;

[Route("accounts")]
[ExceptionHandlerFilter]
public class AccountsController(
    ISendTrialInitiationEmailForRegistrationCommand sendTrialInitiationEmailForRegistrationCommand,
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

        if (token != null)
        {
            return Ok(token);
        }

        return NoContent();
    }
}
