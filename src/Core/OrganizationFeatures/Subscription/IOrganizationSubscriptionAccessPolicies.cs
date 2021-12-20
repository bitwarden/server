using System.Threading.Tasks;
using Bit.Core.AccessPolicies;
using Bit.Core.Models.Business;
using Bit.Core.Models.Table;

namespace Bit.Core.OrganizationFeatures.Subscription
{
    public interface IOrganizationSubscriptionAccessPolicies
    {
        AccessPolicyResult CanCancel(Organization organization);
        AccessPolicyResult CanReinstate(Organization organization);
        AccessPolicyResult CanScale(Organization organization, int seatsToAdd);
        AccessPolicyResult CanAdjustSeats(Organization organization, int seatAdjustment, int currentUsersCount);
        AccessPolicyResult CanAdjustStorage(Organization organization);
        AccessPolicyResult CanUpdateSubscription(Organization organization, int seatAdjustment, int? maxAutoscaleSeats);
        Task<AccessPolicyResult> CanUpgradePlanAsync(Organization organization, OrganizationUpgrade upgrade);
        AccessPolicyResult CanUpdateAutoscaling(Organization organization, int? maxAutoscaleSeats);
    }
}
