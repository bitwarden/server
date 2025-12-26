using Bit.Api.Models.Request;
using Bit.Api.Models.Request.Accounts;
using Bit.Api.Models.Response;
using Bit.Api.Utilities;
using Bit.Core;
using Bit.Core.Billing.Models;
using Bit.Core.Billing.Models.Business;
using Bit.Core.Billing.Services;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Billing.Controllers;

[Route("accounts")]
[Authorize("Application")]
public class AccountsController(
    IUserService userService,
    IFeatureService featureService,
    ILicensingService licensingService) : Controller
{
    // TODO: Migrate to Query / AccountBillingVNextController as part of Premium -> Organization upgrade work.
    [HttpGet("subscription")]
    public async Task<SubscriptionResponseModel> GetSubscriptionAsync(
        [FromServices] GlobalSettings globalSettings,
        [FromServices] IStripePaymentService paymentService)
    {
        var user = await userService.GetUserByPrincipalAsync(User);
        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }

        // Only cloud-hosted users with payment gateways have subscription and discount information
        if (!globalSettings.SelfHosted)
        {
            if (user.Gateway != null)
            {
                // Note: PM23341_Milestone_2 is the feature flag for the overall Milestone 2 initiative (PM-23341).
                // This specific implementation (PM-26682) adds discount display functionality as part of that initiative.
                // The feature flag controls the broader Milestone 2 feature set, not just this specific task.
                var includeMilestone2Discount = featureService.IsEnabled(FeatureFlagKeys.PM23341_Milestone_2);
                var subscriptionInfo = await paymentService.GetSubscriptionAsync(user);
                var license = await userService.GenerateLicenseAsync(user, subscriptionInfo);
                var claimsPrincipal = licensingService.GetClaimsPrincipalFromLicense(license);
                return new SubscriptionResponseModel(user, subscriptionInfo, license, claimsPrincipal, includeMilestone2Discount);
            }
            else
            {
                var license = await userService.GenerateLicenseAsync(user);
                var claimsPrincipal = licensingService.GetClaimsPrincipalFromLicense(license);
                return new SubscriptionResponseModel(user, null, license, claimsPrincipal);
            }
        }
        else
        {
            return new SubscriptionResponseModel(user);
        }
    }

    // TODO: Migrate to Command / AccountBillingVNextController as PUT /account/billing/vnext/subscription
    [HttpPost("storage")]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task<PaymentResponseModel> PostStorageAsync([FromBody] StorageRequestModel model)
    {
        var user = await userService.GetUserByPrincipalAsync(User);
        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }

        var result = await userService.AdjustStorageAsync(user, model.StorageGbAdjustment!.Value);
        return new PaymentResponseModel { Success = true, PaymentIntentClientSecret = result };
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
            new OffboardingSurveyResponse { UserId = user.Id, Reason = request.Reason, Feedback = request.Feedback },
            user.IsExpired());
    }

    // TODO: Migrate to Command / AccountBillingVNextController as POST /account/billing/vnext/subscription/reinstate
    [HttpPost("reinstate-premium")]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task PostReinstateAsync()
    {
        var user = await userService.GetUserByPrincipalAsync(User);
        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }

        await userService.ReinstatePremiumAsync(user);
    }

    private async Task<IEnumerable<Guid>> GetOrganizationIdsClaimingUserAsync(Guid userId)
    {
        var organizationsClaimingUser = await userService.GetOrganizationsClaimingUserAsync(userId);
        return organizationsClaimingUser.Select(o => o.Id);
    }
}
