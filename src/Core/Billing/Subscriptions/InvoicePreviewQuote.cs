using Bit.Core.Billing.Extensions;
using Stripe;

namespace Bit.Core.Billing.Subscriptions;

/// <summary>
/// Rules for treating an invoice preview as a quote pinned to a proration date.
/// </summary>
public static class InvoicePreviewQuote
{
    public static readonly TimeSpan ValidFor = TimeSpan.FromMinutes(30);

    /// <summary>
    /// The current time (the test clock's frozen time when present), in UTC, truncated to whole seconds.
    /// </summary>
    public static DateTime GetProrationDate(Subscription subscription) =>
        Truncate(GetNow(subscription));

    /// <summary>
    /// Throws <see cref="InvoicePreviewExpiredException"/> if <paramref name="prorationDate"/> is in the future,
    /// older than <see cref="ValidFor"/>, or before the subscription's current period start.
    /// </summary>
    public static void EnsureValid(DateTime prorationDate, Subscription subscription)
    {
        var time = prorationDate.Kind switch
        {
            DateTimeKind.Unspecified => DateTime.SpecifyKind(prorationDate, DateTimeKind.Utc),
            DateTimeKind.Local => prorationDate.ToUniversalTime(),
            _ => prorationDate
        };

        var now = GetNow(subscription);

        if (time > now || now - time > ValidFor)
        {
            throw new InvoicePreviewExpiredException();
        }

        var periodStart = subscription.GetCurrentPeriodStart();

        if (periodStart.HasValue && time < periodStart.Value)
        {
            throw new InvoicePreviewExpiredException();
        }
    }

    private static DateTime GetNow(Subscription subscription) =>
        subscription.TestClock?.FrozenTime ?? DateTime.UtcNow;

    private static DateTime Truncate(DateTime value) =>
        new(value.Ticks - value.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc);
}
