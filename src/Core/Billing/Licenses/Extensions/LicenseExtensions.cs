using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Models.Business;

namespace Bit.Core.Billing.Licenses.Extensions;

public static class LicenseExtensions
{

    public static DateTime CalculateFreshExpirationDate(this Organization org, SubscriptionInfo subscriptionInfo)
    {
        if (subscriptionInfo?.Subscription == null)
        {
            if (org.PlanType == PlanType.Custom && org.ExpirationDate.HasValue)
            {
                return org.ExpirationDate.Value;
            }

            return DateTime.UtcNow.AddDays(7);
        }

        var subscription = subscriptionInfo.Subscription;

        if (subscription.TrialEndDate > DateTime.UtcNow)
        {
            return subscription.TrialEndDate.Value;
        }

        if (org.ExpirationDate.HasValue && org.ExpirationDate.Value < DateTime.UtcNow)
        {
            return org.ExpirationDate.Value;
        }

        if (subscription.PeriodEndDate.HasValue && subscription.PeriodDuration > TimeSpan.FromDays(180))
        {
            return subscription.PeriodEndDate
                .Value
                .AddDays(Bit.Core.Constants.OrganizationSelfHostSubscriptionGracePeriodDays);
        }

        return org.ExpirationDate?.AddMonths(11) ?? DateTime.UtcNow.AddYears(1);
    }

    public static DateTime CalculateFreshRefreshDate(this Organization org, SubscriptionInfo subscriptionInfo, DateTime expirationDate)
    {
        if (subscriptionInfo?.Subscription == null ||
            subscriptionInfo.Subscription.TrialEndDate > DateTime.UtcNow ||
            org.ExpirationDate < DateTime.UtcNow)
        {
            return expirationDate;
        }

        return subscriptionInfo.Subscription.PeriodDuration > TimeSpan.FromDays(180) ||
            DateTime.UtcNow - expirationDate > TimeSpan.FromDays(30)
            ? DateTime.UtcNow.AddDays(30)
            : expirationDate;
    }

    public static DateTime CalculateFreshExpirationDateWithoutGracePeriod(this Organization org, SubscriptionInfo subscriptionInfo, DateTime expirationDate)
    {
        if (subscriptionInfo?.Subscription is null)
        {
            return expirationDate;
        }

        var subscription = subscriptionInfo.Subscription;

        if (subscription.TrialEndDate <= DateTime.UtcNow &&
            org.ExpirationDate >= DateTime.UtcNow &&
            subscription.PeriodEndDate.HasValue &&
            subscription.PeriodDuration > TimeSpan.FromDays(180))
        {
            return subscription.PeriodEndDate.Value;
        }

        return expirationDate;
    }
}
