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
    /// Stripe stores proration dates as unix seconds, so the value returned to the client must match what Stripe used exactly.
    /// </summary>
    public static DateTime GetProrationDate(Subscription subscription, TimeProvider timeProvider) =>
        Truncate(GetNow(subscription, timeProvider));

    /// <summary>
    /// Throws <see cref="InvoicePreviewExpiredException"/> if <paramref name="prorationDate"/> is in the future,
    /// older than <see cref="ValidFor"/>, or before the subscription's current period start.
    /// Returns the date normalized to UTC; pass that value (not the input) to Stripe so the instant that was
    /// checked is the instant that is sent.
    /// </summary>
    public static DateTime EnsureValid(DateTime prorationDate, Subscription subscription, TimeProvider timeProvider)
    {
        var time = prorationDate.Kind switch
        {
            DateTimeKind.Unspecified => DateTime.SpecifyKind(prorationDate, DateTimeKind.Utc),
            DateTimeKind.Local => prorationDate.ToUniversalTime(),
            _ => prorationDate
        };

        var now = GetNow(subscription, timeProvider);

        if (time > now || now - time > ValidFor)
        {
            throw new InvoicePreviewExpiredException();
        }

        var periodStart = subscription.GetCurrentPeriodStart();

        if (periodStart.HasValue && time < periodStart.Value)
        {
            throw new InvoicePreviewExpiredException();
        }

        return time;
    }

    private static DateTime GetNow(Subscription subscription, TimeProvider timeProvider) =>
        subscription.TestClock?.FrozenTime ?? timeProvider.GetUtcNow().UtcDateTime;

    private static DateTime Truncate(DateTime value) =>
        new(value.Ticks - value.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc);
}
