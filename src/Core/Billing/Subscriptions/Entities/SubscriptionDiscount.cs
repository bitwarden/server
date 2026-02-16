#nullable enable

using System.ComponentModel.DataAnnotations;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Utilities;

namespace Bit.Core.Billing.Subscriptions.Entities;

public class SubscriptionDiscount : ITableObject<Guid>, IRevisable, IValidatableObject
{
    public Guid Id { get; set; }
    [MaxLength(50)]
    public string StripeCouponId { get; set; } = null!;
    public ICollection<string>? StripeProductIds { get; set; }
    public decimal? PercentOff { get; set; }
    public long? AmountOff { get; set; }
    [MaxLength(10)]
    public string? Currency { get; set; }
    [MaxLength(20)]
    public string Duration { get; set; } = null!;
    public int? DurationInMonths { get; set; }
    [MaxLength(100)]
    public string? Name { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DiscountAudienceType AudienceType { get; set; }
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public DateTime RevisionDate { get; set; } = DateTime.UtcNow;

    public void SetNewId()
    {
        if (Id == default)
        {
            Id = CoreHelpers.GenerateComb();
        }
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EndDate < StartDate)
        {
            yield return new ValidationResult(
                "EndDate must be greater than or equal to StartDate.",
                new[] { nameof(EndDate) });
        }
    }
}
