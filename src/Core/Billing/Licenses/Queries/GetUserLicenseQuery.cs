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
    IStripePaymentService paymentService) : IGetUserLicenseQuery
{
    public async Task<UserLicense> Run(User user)
    {
        var subscriptionInfo = await paymentService.GetSubscriptionAsync(user);
        SubscriptionLicenseValidator.ValidateSubscriptionForLicenseGeneration(subscriptionInfo);

        return await userService.GenerateLicenseAsync(user);
    }
}
