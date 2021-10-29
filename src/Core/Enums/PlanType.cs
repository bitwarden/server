using System.ComponentModel.DataAnnotations;
using System.Linq;
using Bit.Core.Models.Table;

namespace Bit.Core.Enums
{
    public enum PlanType : byte
    {
        [Display(Name = "Free")]
        Free = 0,
        [Display(Name = "Families 2019")]
        FamiliesAnnually2019 = 1,
        [Display(Name = "Teams (Monthly) 2019")]
        TeamsMonthly2019 = 2,
        [Display(Name = "Teams (Annually) 2019")]
        TeamsAnnually2019 = 3,
        [Display(Name = "Enterprise (Monthly) 2019")]
        EnterpriseMonthly2019 = 4,
        [Display(Name = "Enterprise (Annually) 2019")]
        EnterpriseAnnually2019 = 5,
        [Display(Name = "Custom")]
        Custom = 6,
        [Display(Name = "Families")]
        FamiliesAnnually = 7,
        [Display(Name = "Teams (Monthly)")]
        TeamsMonthly = 8,
        [Display(Name = "Teams (Annually)")]
        TeamsAnnually = 9,
        [Display(Name = "Enterprise (Monthly)")]
        EnterpriseMonthly = 10,
        [Display(Name = "Enterprise (Annually)")]
        EnterpriseAnnually= 11,
    }

    public static class PlanTypeHelper
    {
        private static readonly PlanType[] _freePlans = new[] { PlanType.Free };
        private static readonly PlanType[] _familiesPlans = new[] { PlanType.FamiliesAnnually, PlanType.FamiliesAnnually2019 };
        private static readonly PlanType[] _teamsPlans = new[] { PlanType.TeamsAnnually, PlanType.TeamsAnnually2019,
            PlanType.TeamsMonthly, PlanType.TeamsMonthly2019};
        private static readonly PlanType[] _enterprisePlans = new[] { PlanType.EnterpriseAnnually,
            PlanType.EnterpriseAnnually2019, PlanType.EnterpriseMonthly, PlanType.EnterpriseMonthly2019 };

        private static bool HasPlan(PlanType[] planTypes, PlanType planType) => planTypes.Any(p => p == planType);
        public static bool HasFreePlan(Organization org) => IsFree(org.PlanType);
        public static bool IsFree(PlanType planType) => HasPlan(_freePlans, planType);
        public static bool HasFamiliesPlan(Organization org) => IsFamilies(org.PlanType);
        public static bool IsFamilies(PlanType planType) => HasPlan(_familiesPlans, planType);
        public static bool HasTeamsPlan(Organization org) => IsTeams(org.PlanType);
        public static bool IsTeams(PlanType planType) => HasPlan(_teamsPlans, planType);
        public static bool HasEnterprisePlan(Organization org) => IsEnterprise(org.PlanType);
        public static bool IsEnterprise(PlanType planType) => HasPlan(_enterprisePlans, planType);
    }
}
