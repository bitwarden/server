using Bit.Core.AdminConsole.Entities;
using Bit.Core.Enums;

namespace Bit.Core.Billing.Extensions;

public static class BillingExtensions
{
    public static bool IsStripeEnabled(this Organization organization)
        => !string.IsNullOrEmpty(organization.GatewayCustomerId) &&
           !string.IsNullOrEmpty(organization.GatewaySubscriptionId);

    public static bool SupportsConsolidatedBilling(this PlanType planType)
        => planType is PlanType.TeamsMonthly or PlanType.EnterpriseMonthly;
}
