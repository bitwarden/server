using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Enums
{
    public enum PlanType : byte
    {
        [Display(Name = "Free")]
        Free = 0,
        [Display(Name = "Families")]
        FamiliesAnnually = 1,
        [Display(Name = "Teams (Monthly)")]
        TeamsMonthly = 2,
        [Display(Name = "Teams (Annually)")]
        TeamsAnnually = 3,
        [Display(Name = "Enterprise (Monthly)")]
        EnterpriseMonthly = 4,
        [Display(Name = "Enterprise (Annually)")]
        EnterpriseAnnually = 5,
        [Display(Name = "Custom")]
        Custom = 6,
        [Display(Name = "PLACEHOLDER")]
        SsoPlaceholderMonthly = 10,
        [Display(Name = "PLACEHOLDER")]
        SsoPlaceholderAnnually = 11,
    }
}
