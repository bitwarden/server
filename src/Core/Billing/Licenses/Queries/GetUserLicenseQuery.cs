using Bit.Core.Billing.Models.Business;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Services;

namespace Bit.Core.Billing.Licenses.Queries;

public interface IGetUserLicenseQuery
{
    Task<UserLicense> Run(User user);
}

public class GetUserLicenseQuery(
    IUserService userService,
    IStripeAdapter stripeAdapter) : IGetUserLicenseQuery
{
    public async Task<UserLicense> Run(User user)
    {
        var subscription = string.IsNullOrEmpty(user.GatewaySubscriptionId)
            ? null
            : await stripeAdapter.GetSubscriptionAsync(user.GatewaySubscriptionId);
        SubscriptionLicenseValidator.ValidateSubscriptionForLicenseGeneration(subscription);

        return await userService.GenerateLicenseAsync(user);
    }
}
