using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Extensions;
using Bit.Core.Models.Business;
using Stripe;
using Plan = Bit.Core.Models.StaticStore.Plan;

namespace Bit.Core.Billing.Organizations.Helpers;

public static class PrivilegedControlsTrialHelpers
{
    /// <summary>
    /// An organization is on a sales-initiated Privileged Controls trial when its plan supports Privileged Controls,
    /// Privileged Controls is on, and its subscription (if it has one) carries no Privileged Controls seat line.
    /// </summary>
    public static bool IsOnSalesInitiatedTrial(
        Organization organization,
        Plan plan,
        Subscription? subscription) =>
        IsOnSalesInitiatedTrial(organization, plan, subscription?.HasPrivilegedControlsSeats() ?? false);

    /// <inheritdoc cref="IsOnSalesInitiatedTrial(Organization, Plan, Subscription)"/>
    public static bool IsOnSalesInitiatedTrial(
        Organization organization,
        Plan plan,
        SubscriptionInfo? subscriptionInfo = null) =>
        IsOnSalesInitiatedTrial(
            organization, plan, subscriptionInfo?.Subscription?.HasPrivilegedControlsSeats ?? false);

    private static bool IsOnSalesInitiatedTrial(Organization organization, Plan plan, bool hasPrivilegedControlsSeats) =>
        plan.SupportsPrivilegedControls &&
        organization.UsePam &&
        !hasPrivilegedControlsSeats;
}
