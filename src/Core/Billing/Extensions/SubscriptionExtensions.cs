using System.Globalization;
using Bit.Core.Billing.Constants;
using Stripe;

namespace Bit.Core.Billing.Extensions;

public static class SubscriptionExtensions
{
    public static int GetMigrationGraceServiceAccounts(this Subscription subscription) =>
        subscription.Metadata != null
        && subscription.Metadata.TryGetValue(StripeConstants.MetadataKeys.MigrationGraceServiceAccounts, out var raw)
        && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var grace)
        && grace > 0
            ? grace
            : 0;

    /*
     * For the time being, this is the simplest migration approach from v45 to v48 as
     * we do not support multi-cadence subscriptions. Each subscription item should be on the
     * same billing cycle. If this changes, we'll need a significantly more robust approach.
     *
     * Because we can't guarantee a subscription will have items, this has to be nullable.
     */
    public static (DateTime? Start, DateTime? End)? GetCurrentPeriod(this Subscription subscription)
    {
        var item = subscription.Items?.FirstOrDefault();
        return item is null ? null : (item.CurrentPeriodStart, item.CurrentPeriodEnd);
    }

    public static DateTime? GetCurrentPeriodStart(this Subscription subscription) =>
        subscription.Items?.FirstOrDefault()?.CurrentPeriodStart;

    public static DateTime? GetCurrentPeriodEnd(this Subscription subscription) =>
        subscription.Items?.FirstOrDefault()?.CurrentPeriodEnd;
}
