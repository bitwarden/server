#nullable enable
using System.ComponentModel.DataAnnotations;

namespace Bit.Core.NotificationCenter.Enums;

public enum Priority : byte
{
    [Display(Name = "Critical")]
    Critical = 0,
    [Display(Name = "High")]
    High = 1,
    [Display(Name = "Medium")]
    Medium = 2,
    [Display(Name = "Low")]
    Low = 3,
    [Display(Name = "Informational")]
    Informational = 4
}
