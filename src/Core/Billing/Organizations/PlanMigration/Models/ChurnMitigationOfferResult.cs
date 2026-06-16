namespace Bit.Core.Billing.Organizations.PlanMigration.Models;

public sealed record ChurnMitigationOfferResult(
    string CouponId,
    decimal? PercentOff,
    decimal? AmountOff,
    string Duration,
    int? DurationInMonths,
    string Name);
