using Bit.Core.Billing.Enums;

namespace Bit.Admin.Billing.Models;

public class SubscriptionDiscountViewModel
{
    public Guid Id { get; set; }
    public string StripeCouponId { get; set; } = null!;
    public string? Name { get; set; }
    public decimal? PercentOff { get; set; }
    public long? AmountOff { get; set; }
    public string? Currency { get; set; }
    public string Duration { get; set; } = null!;
    public int? DurationInMonths { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DiscountAudienceType AudienceType { get; set; }
    public DateTime CreationDate { get; set; }
    public bool IsActive => DateTime.UtcNow >= StartDate && DateTime.UtcNow <= EndDate;

    public string DiscountDisplay => PercentOff.HasValue
        ? $"{PercentOff.Value:G29}% off"
        : $"${AmountOff / 100m} off";

    public bool IsRestrictedToNewUsersOnly => AudienceType == DiscountAudienceType.UserHasNoPreviousSubscriptions;
    public bool IsAvailableToAllUsers => AudienceType == DiscountAudienceType.AllUsers;
}
