using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Enums;

public enum ProductType : byte
{
    [Display(Name = "Free")]
    Free = 0,
    [Display(Name = "Families")]
    Families = 1,
    [Display(Name = "Teams")]
    Teams = 2,
    [Display(Name = "Enterprise")]
    Enterprise = 3,
}

