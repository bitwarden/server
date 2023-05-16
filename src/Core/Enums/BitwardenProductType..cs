using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Enums;

public enum BitwardenProductType  : byte
{
    [Display(Name = "PasswordManager")]
    PasswordManager = 0,
    [Display(Name = "SecretManager")]
    SecretManager = 1,
}
