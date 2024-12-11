#nullable enable
using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Enums;

public enum ClientType : byte
{
    [Display(Name = "All")]
    All = 0,

    [Display(Name = "Web Vault")]
    Web = 1,

    [Display(Name = "Browser Extension")]
    Browser = 2,

    [Display(Name = "Desktop App")]
    Desktop = 3,

    [Display(Name = "Mobile App")]
    Mobile = 4,
}
