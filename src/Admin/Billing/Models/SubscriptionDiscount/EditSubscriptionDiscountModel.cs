using System.ComponentModel.DataAnnotations;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Subscriptions.Entities;

namespace Bit.Admin.Billing.Models;

public class EditSubscriptionDiscountModel : IValidatableObject
{
    public Guid Id { get; set; }

    public string StripeCouponId { get; set; } = null!;
    public string? Name { get; set; }
    public decimal? PercentOff { get; set; }
    public long? AmountOff { get; set; }
    public string? Currency { get; set; }
    public string Duration { get; set; } = string.Empty;
    public int? DurationInMonths { get; set; }
    public ICollection<string>? StripeProductIds { get; set; }
    public Dictionary<string, string>? AppliesToProducts { get; set; } // Key: ProductId, Value: ProductName

    [Required]
    [Display(Name = "Start Date")]
    public DateTime StartDate { get; set; }

    [Required]
    [Display(Name = "End Date")]
    public DateTime EndDate { get; set; }

    [Display(Name = "Restrict to users with no previous subscriptions?")]
    public bool RestrictToNewUsersOnly { get; set; }

    public DiscountAudienceType AudienceType => RestrictToNewUsersOnly
        ? DiscountAudienceType.UserHasNoPreviousSubscriptions
        : DiscountAudienceType.AllUsers;

    public EditSubscriptionDiscountModel() { }

    public EditSubscriptionDiscountModel(SubscriptionDiscount discount)
    {
        Id = discount.Id;
        StripeCouponId = discount.StripeCouponId;
        Name = discount.Name;
        PercentOff = discount.PercentOff;
        AmountOff = discount.AmountOff;
        Currency = discount.Currency;
        Duration = discount.Duration;
        DurationInMonths = discount.DurationInMonths;
        StripeProductIds = discount.StripeProductIds;
        StartDate = discount.StartDate;
        EndDate = discount.EndDate;
        RestrictToNewUsersOnly = discount.AudienceType == DiscountAudienceType.UserHasNoPreviousSubscriptions;
    }

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
