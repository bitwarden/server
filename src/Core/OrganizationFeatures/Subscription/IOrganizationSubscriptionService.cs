using System;
using System.Threading.Tasks;
using Bit.Core.Models.Business;
using Bit.Core.Models.Table;

namespace Bit.Core.OrganizationFeatures.Subscription
{
    public interface IOrganizationSubscriptionService
    {
        Task CancelSubscriptionAsync(Guid organizationId, bool? endOfPeriod = null);
        Task ReinstateSubscriptionAsync(Guid organizationId);
        Task UpdateSubscription(Guid organizationId, int seatAdjustment, int? maxAutoscaleSeats);
        Task<(bool success, string paymentIntentClientSecret)> UpgradePlanAsync(Guid organizationId, OrganizationUpgrade upgrade);
        Task AutoAddSeatsAsync(Organization organization, int newSeatsRequired, DateTime? prorationDate = null);
        Task<string> AdjustSeatsAsync(Organization organization, int seatAdjustment, DateTime? prorationDate = null);
        Task<string> AdjustStorageAsync(Guid organizationId, short storageAdjustmentGb);
    }
}
