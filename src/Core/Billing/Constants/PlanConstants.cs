using Bit.Core.Billing.Enums;

namespace Bit.Core.Billing.Constants;

public static class PlanConstants
{
    /// <summary>The most of any one purchasable item, such as seats or service accounts, that can be bought at once.</summary>
    public const int MaxPurchasableQuantity = 100_000;

    public static List<PlanType> EnterprisePlanTypes =>
    [
        PlanType.EnterpriseAnnually2019,
        PlanType.EnterpriseAnnually2020,
        PlanType.EnterpriseAnnually2023,
        PlanType.EnterpriseAnnually,
        PlanType.EnterpriseMonthly2019,
        PlanType.EnterpriseMonthly2020,
        PlanType.EnterpriseMonthly2023,
        PlanType.EnterpriseMonthly
    ];

    public static List<PlanType> TeamsPlanTypes =>
    [
        PlanType.TeamsAnnually2019,
        PlanType.TeamsAnnually2020,
        PlanType.TeamsAnnually2023,
        PlanType.TeamsAnnually,
        PlanType.TeamsMonthly2019,
        PlanType.TeamsMonthly2020,
        PlanType.TeamsMonthly2023,
        PlanType.TeamsMonthly
    ];

    public static List<PlanType> FamiliesPlanTypes =>
    [
        PlanType.FamiliesAnnually2019,
        PlanType.FamiliesAnnually2025,
        PlanType.FamiliesAnnually
    ];
}
