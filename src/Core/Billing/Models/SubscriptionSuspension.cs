namespace Bit.Core.Billing.Models;

public record SubscriptionSuspension(
    DateTime SuspensionDate,
    DateTime UnpaidPeriodEndDate,
    int GracePeriod);
