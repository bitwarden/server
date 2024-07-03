namespace Bit.Core.Billing.Models;

public record SubscriptionSuspensionDTO(
    DateTime SuspensionDate,
    DateTime UnpaidPeriodEndDate,
    int GracePeriod);
