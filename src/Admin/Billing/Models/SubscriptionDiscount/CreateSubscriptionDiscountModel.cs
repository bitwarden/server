using System.ComponentModel.DataAnnotations;
using Bit.Core.Billing.Enums;

namespace Bit.Admin.Billing.Models;

public class CreateSubscriptionDiscountModel : IValidatableObject
{
    [Required]
    [Display(Name = "Stripe Coupon ID")]
    [MaxLength(50)]
    public string StripeCouponId { get; set; } = null!;

    public string? Name { get; set; }
    public decimal? PercentOff { get; set; }
    public long? AmountOff { get; set; }
    public string? Currency { get; set; }
    public string Duration { get; set; } = string.Empty;
    public int? DurationInMonths { get; set; }
    public Dictionary<string, string>? AppliesToProducts { get; set; } // Key: ProductId, Value: ProductName

    [Required]
    [Display(Name = "Start Date")]
    public DateTime StartDate { get; set; } = DateTime.UtcNow.Date;

    [Required]
    [Display(Name = "End Date")]
    public DateTime EndDate { get; set; } = DateTime.UtcNow.Date.AddMonths(1);

    [Display(Name = "Restrict to users with no previous subscriptions?")]
    public bool RestrictToNewUsersOnly { get; set; }

    public DiscountAudienceType AudienceType => RestrictToNewUsersOnly
        ? DiscountAudienceType.UserHasNoPreviousSubscriptions
        : DiscountAudienceType.AllUsers;

    public bool IsImported { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EndDate < StartDate)
        {
            yield return new ValidationResult(
                "End Date must be on or after Start Date.",
                new[] { nameof(EndDate) });
        }
    }
}
