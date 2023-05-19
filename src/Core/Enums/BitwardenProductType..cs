using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Enums;

public enum BitwardenProductType : byte
{
    [Display(Name = "Password Manager")]
    PasswordManager = 0,
    [Display(Name = "Secrets Manager")]
    SecretsManager = 1,
}
