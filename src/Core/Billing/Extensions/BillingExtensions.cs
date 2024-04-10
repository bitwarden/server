using Bit.Core.Enums;

namespace Bit.Core.Billing.Extensions;

public static class BillingExtensions
{
    public static bool SupportsConsolidatedBilling(this PlanType planType)
        => planType is PlanType.TeamsMonthly or PlanType.EnterpriseMonthly;
}
