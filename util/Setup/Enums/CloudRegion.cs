using System.ComponentModel.DataAnnotations;

namespace Bit.Setup.Enums;

public enum CloudRegion
{
    [Display(Name = "US")]
    US = 0,
    [Display(Name = "EU")]
    EU = 1,
}
