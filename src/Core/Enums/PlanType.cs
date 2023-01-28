using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Enums;

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
    EnterpriseAnnually = 11,
}
