using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Subscriptions;
using Stripe;
using Stripe.TestHelpers;
using Xunit;

namespace Bit.Core.Test.Billing.Subscriptions;

public class InvoicePreviewQuoteTests
{
    private static readonly DateTime _frozen = new(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc);

    private static Subscription CreateSubscription(DateTime? frozenTime = null, DateTime? periodStart = null) => new()
    {
        TestClock = new TestClock { FrozenTime = frozenTime ?? _frozen },
        Items = new StripeList<SubscriptionItem>
        {
            Data = [new SubscriptionItem { CurrentPeriodStart = periodStart ?? _frozen.AddDays(-180) }]
        }
    };

    [Fact]
    public void GetProrationDate_TestClock_ReturnsFrozenTime() =>
        Assert.Equal(_frozen, InvoicePreviewQuote.GetProrationDate(CreateSubscription()));

    [Fact]
    public void GetProrationDate_FrozenTimeHasMilliseconds_TruncatesToSecond() =>
        Assert.Equal(_frozen,
            InvoicePreviewQuote.GetProrationDate(CreateSubscription(_frozen.AddMilliseconds(750))));

    [Fact]
    public void GetProrationDate_NoTestClock_ReturnsUtcNowTruncated()
    {
        var result = InvoicePreviewQuote.GetProrationDate(new Subscription());

        Assert.Equal(DateTimeKind.Utc, result.Kind);
        Assert.Equal(0, result.Ticks % TimeSpan.TicksPerSecond);
        Assert.True(Math.Abs((DateTime.UtcNow - result).TotalSeconds) < 2);
    }

    [Fact]
    public void EnsureValid_EqualToNow_Accepts() =>
        InvoicePreviewQuote.EnsureValid(_frozen, CreateSubscription());

    [Fact]
    public void EnsureValid_ExactlyThirtyMinutesOld_Accepts() =>
        InvoicePreviewQuote.EnsureValid(_frozen.AddMinutes(-30), CreateSubscription());

    [Fact]
    public void EnsureValid_ThirtyMinutesAndOneSecondOld_Throws() =>
        Assert.Throws<InvoicePreviewExpiredException>(() =>
            InvoicePreviewQuote.EnsureValid(_frozen.AddMinutes(-30).AddSeconds(-1), CreateSubscription()));

    [Fact]
    public void EnsureValid_InTheFuture_Throws() =>
        Assert.Throws<InvoicePreviewExpiredException>(() =>
            InvoicePreviewQuote.EnsureValid(_frozen.AddSeconds(1), CreateSubscription()));

    [Fact]
    public void EnsureValid_BeforeCurrentPeriodStart_Throws() =>
        Assert.Throws<InvoicePreviewExpiredException>(() =>
            InvoicePreviewQuote.EnsureValid(_frozen.AddMinutes(-20),
                CreateSubscription(periodStart: _frozen.AddMinutes(-10))));

    [Fact]
    public void EnsureValid_NoItems_SkipsPeriodCheck()
    {
        var subscription = CreateSubscription();
        subscription.Items = new StripeList<SubscriptionItem> { Data = [] };

        InvoicePreviewQuote.EnsureValid(_frozen.AddMinutes(-5), subscription);
    }

    [Fact]
    public void EnsureValid_UnspecifiedKind_ReturnsSameInstantAsUtc()
    {
        var input = DateTime.SpecifyKind(_frozen.AddMinutes(-5), DateTimeKind.Unspecified);

        var result = InvoicePreviewQuote.EnsureValid(input, CreateSubscription());

        Assert.Equal(DateTimeKind.Utc, result.Kind);
        Assert.Equal(input.Ticks, result.Ticks);
    }

    [Fact]
    public void EnsureValid_LocalKind_ReturnsSameInstantAsUtc()
    {
        var input = _frozen.AddMinutes(-5).ToLocalTime();
        Assert.Equal(DateTimeKind.Local, input.Kind);

        var result = InvoicePreviewQuote.EnsureValid(input, CreateSubscription());

        Assert.Equal(DateTimeKind.Utc, result.Kind);
        Assert.Equal(_frozen.AddMinutes(-5), result);
    }

    [Fact]
    public void InvoicePreviewExpiredException_IsKeyedBadRequest()
    {
        var exception = new InvoicePreviewExpiredException();

        Assert.True(exception.ModelState!.ContainsKey(BillingErrorKeys.InvoicePreviewExpired));
        Assert.Equal("InvoicePreviewExpired", BillingErrorKeys.InvoicePreviewExpired);
    }
}
