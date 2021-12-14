using Bit.Core.AccessPolicies;
using Bit.Core.Models.Table;

namespace Bit.Core.OrganizationFeatures.Subscription
{
    public interface IOrganizationSubscriptionAccessPolicies
    {
        AccessPolicyResult CanScale(Organization organization, int seatsToAdd);
        AccessPolicyResult CanAdjustSeats(Organization organization, int seatAdjustment, int currentUsersCount);
        AccessPolicyResult CanUpdateSubscription(Organization organization, int seatAdjustment, int? maxAutoscaleSeats);
        AccessPolicyResult CanUpdateAutoscaling(Organization organization, int? maxAutoscaleSeats);
    }
}
