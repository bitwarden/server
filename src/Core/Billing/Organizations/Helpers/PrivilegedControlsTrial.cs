using Bit.Core.AdminConsole.Entities;
using Bit.Core.Models.Business;
using Plan = Bit.Core.Models.StaticStore.Plan;

namespace Bit.Core.Billing.Organizations.Helpers;

public static class PrivilegedControlsTrial
{
    /// <summary>
    /// An organization is on a sales-initiated Privileged Controls trial when its plan supports Privileged Controls,
    /// Privileged Controls is on, and its subscription (if it has one) carries no Privileged Controls seat line.
    /// </summary>
    public static bool IsSalesInitiated(
        Organization organization,
        Plan plan,
        SubscriptionInfo? subscriptionInfo = null) =>
        plan.SupportsPrivilegedControls &&
        organization.UsePam &&
        subscriptionInfo?.Subscription?.HasPrivilegedControlsSeats != true;
}
