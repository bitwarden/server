using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Billing.Enums;

public enum ProductTierType : byte
{
    [Display(Name = "Free")]
    Free = 0,

    [Display(Name = "Families")]
    Families = 1,

    [Display(Name = "Teams")]
    Teams = 2,

    [Display(Name = "Enterprise")]
    Enterprise = 3,

    [Display(Name = "Teams Starter")]
    TeamsStarter = 4,
}
