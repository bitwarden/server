using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Enums;

public enum TransactionType : byte
{
    [Display(Name = "Charge")]
    Charge = 0,

    [Display(Name = "Credit")]
    Credit = 1,

    [Display(Name = "Promotional Credit")]
    PromotionalCredit = 2,

    [Display(Name = "Referral Credit")]
    ReferralCredit = 3,

    [Display(Name = "Refund")]
    Refund = 4,
}
