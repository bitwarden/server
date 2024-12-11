#nullable enable
using System.ComponentModel.DataAnnotations;

namespace Bit.Core.NotificationCenter.Enums;

public enum Priority : byte
{
    [Display(Name = "Informational")]
    Informational = 0,

    [Display(Name = "Low")]
    Low = 1,

    [Display(Name = "Medium")]
    Medium = 2,

    [Display(Name = "High")]
    High = 3,

    [Display(Name = "Critical")]
    Critical = 4,
}
