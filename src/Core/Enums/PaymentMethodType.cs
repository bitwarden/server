using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Enums;

public enum PaymentMethodType : byte
{
    [Display(Name = "Card")]
    Card = 0,

    [Display(Name = "Bank Account")]
    BankAccount = 1,

    [Display(Name = "PayPal")]
    PayPal = 2,

    [Display(Name = "BitPay")]
    BitPay = 3,

    [Display(Name = "Credit")]
    Credit = 4,

    [Display(Name = "Wire Transfer")]
    WireTransfer = 5,

    [Display(Name = "Check")]
    Check = 8,

    [Display(Name = "None")]
    None = 255,
}
