using Bit.Core.Billing.Enums;

namespace Bit.Core.Billing.Organizations.AnnualUpgradeOffer;

public static class AnnualUpgradeOfferPlans
{
    private static readonly Dictionary<PlanType, PlanType> MonthlyToAnnualLatest = new()
    {
        [PlanType.TeamsMonthly2019] = PlanType.TeamsAnnually,
        [PlanType.TeamsMonthly2020] = PlanType.TeamsAnnually,
        [PlanType.TeamsMonthly2023] = PlanType.TeamsAnnually,
        [PlanType.TeamsMonthly] = PlanType.TeamsAnnually,
        [PlanType.EnterpriseMonthly2019] = PlanType.EnterpriseAnnually,
        [PlanType.EnterpriseMonthly2020] = PlanType.EnterpriseAnnually,
        [PlanType.EnterpriseMonthly2023] = PlanType.EnterpriseAnnually,
        [PlanType.EnterpriseMonthly] = PlanType.EnterpriseAnnually,
    };

    /// <summary>
    /// Resolves the current-vintage annual plan an organization on <paramref name="current"/>
    /// would move to via the annual-upgrade offer -- the same target the price-migration program
    /// would eventually move the organization to, regardless of which monthly vintage it's on today.
    /// </summary>
    public static PlanType? ResolveAnnualLatestPlanType(PlanType current) =>
        MonthlyToAnnualLatest.TryGetValue(current, out var target) ? target : null;
}
