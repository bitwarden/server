using Bit.Core.Billing.Enums;

namespace Bit.Core.Billing.Organizations.Models;

public record OrganizationSubscriptionPlanChange
{
    public ProductTierType Tier { get; init; }
    public PlanCadenceType Cadence { get; init; }

    public PlanType PlanType =>
        // ReSharper disable once SwitchExpressionHandlesSomeKnownEnumValuesWithExceptionInDefault
        Tier switch
        {
            ProductTierType.Families => PlanType.FamiliesAnnually,
            ProductTierType.Teams => Cadence == PlanCadenceType.Monthly
                ? PlanType.TeamsMonthly
                : PlanType.TeamsAnnually,
            ProductTierType.Enterprise => Cadence == PlanCadenceType.Monthly
                ? PlanType.EnterpriseMonthly
                : PlanType.EnterpriseAnnually,
            _ => throw new InvalidOperationException("Cannot change an Organization subscription to a tier that isn't Families, Teams or Enterprise.")
        };
}
