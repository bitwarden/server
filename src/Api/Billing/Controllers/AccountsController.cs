using Bit.Api.Models.Request;
using Bit.Api.Models.Response;
using Bit.Api.Utilities;
using Bit.Core.Billing.Models;
using Bit.Core.Billing.Models.Business;
using Bit.Core.Billing.Services;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Billing.Controllers;

[Route("accounts")]
[Authorize("Application")]
public class AccountsController(
    IUserService userService) : Controller
{
    [HttpGet("subscription")]
    [SelfHosted(SelfHostedOnly = true)]
    public async Task<SubscriptionResponseModel> GetSubscriptionAsync()
    {
        var user = await userService.GetUserByPrincipalAsync(User);
        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }

        return new SubscriptionResponseModel(user);
    }

    /*
     * TODO: A new version of this exists in the AccountBillingVNextController.
     * The individual-self-hosting-license-uploader.component needs to be updated to use it.
     * Then, this can be removed.
     */
    [HttpPost("license")]
    [SelfHosted(SelfHostedOnly = true)]
    public async Task PostLicenseAsync(LicenseRequestModel model)
    {
        var user = await userService.GetUserByPrincipalAsync(User);
        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }

        var license = await ApiHelpers.ReadJsonFileFromBody<UserLicense>(HttpContext, model.License);
        if (license == null)
        {
            throw new BadRequestException("Invalid license");
        }

        await userService.UpdateLicenseAsync(user, license);
    }

    // TODO: Migrate to Command / AccountBillingVNextController as DELETE /account/billing/vnext/subscription
    [HttpPost("cancel")]
    public async Task PostCancelAsync(
        [FromBody] SubscriptionCancellationRequestModel request,
        [FromServices] ISubscriberService subscriberService)
    {
        var user = await userService.GetUserByPrincipalAsync(User);

        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }

        await subscriberService.CancelSubscription(user,
            user.IsExpired(),
            new OffboardingSurveyResponse { UserId = user.Id, Reason = request.Reason, Feedback = request.Feedback });
    }
}
