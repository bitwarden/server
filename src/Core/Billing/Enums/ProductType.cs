using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Billing.Enums;

public enum ProductType
{
    [Display(Name = "Password Manager")]
    PasswordManager = 0,

    [Display(Name = "Secrets Manager")]
    SecretsManager = 1,
}
