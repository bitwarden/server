using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Enums;

public enum CloudRegion
{
    [Display(Name = "US")]
    US = 0,
    [Display(Name = "EU")]
    EU = 1,
}
