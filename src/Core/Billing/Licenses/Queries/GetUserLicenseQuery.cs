using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Models.Business;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Services;

namespace Bit.Core.Billing.Licenses.Queries;

public interface IGetUserLicenseQuery
{
    Task<UserLicense> Run(User user);
}

public class GetUserLicenseQuery(
    IUserService userService,
    IStripePaymentService paymentService) : IGetUserLicenseQuery
{
    public async Task<UserLicense> Run(User user)
    {
        var subscriptionInfo = await paymentService.GetSubscriptionAsync(user);

        if (subscriptionInfo.Subscription is null)
        {
            throw new BadRequestException("No active subscription found.");
        }

        if (subscriptionInfo.Subscription.Status is StripeConstants.SubscriptionStatus.Canceled
            or StripeConstants.SubscriptionStatus.Incomplete
            or StripeConstants.SubscriptionStatus.IncompleteExpired)
        {
            throw new BadRequestException(
                "Unable to generate license due to a payment issue. Please update your billing information or contact support for assistance.");
        }

        return await userService.GenerateLicenseAsync(user);
    }
}
