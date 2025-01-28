using Bit.Core.Billing.Enums;

namespace Bit.Core.Billing.Constants;

public static class PlanConstants
{
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
}
