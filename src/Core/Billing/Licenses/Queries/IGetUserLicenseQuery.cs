using Bit.Core.Entities;
using Bit.Core.Models.Business;

namespace Bit.Core.Billing.Licenses.Queries;

public interface IGetUserLicenseQuery
{
    Task<UserLicense> GetLicenseAsync(User user, SubscriptionInfo subscriptionInfo = null, int? version = null);
}
