using Bit.Invoicing.InvoicePreviews;
using Stripe;
using Xunit;

namespace Bit.Invoicing.Test;

public class ProrationMapperTests
{
    private static InvoiceLineItem Line(long amountCents, DateTime? periodStart = null, DateTime? periodEnd = null, long taxCents = 0) => new()
    {
        Amount = amountCents,
        Period = periodEnd is null ? null : new InvoiceLineItemPeriod { Start = periodStart ?? default, End = periodEnd.Value },
        Taxes = taxCents == 0 ? null : [new InvoiceLineItemTax { Amount = taxCents }],
    };

    [Fact]
    public void Summarize_EmptyBucket_ReturnsNull()
        => Assert.Null(ProrationMapper.Summarize([]));

    [Fact]
    public void Summarize_SplitsChargeCreditAndNet_InDollars()
    {
        var result = ProrationMapper.Summarize([Line(7_355), Line(-3_773)])!;
        Assert.Equal(73.55m, result.Charge);
        Assert.Equal(37.73m, result.Credit);
        Assert.Equal(35.82m, result.Total);
    }

    [Fact]
    public void Summarize_EntirelyNegativeBucket_IsAllCredit()
    {
        var result = ProrationMapper.Summarize([Line(-2_507), Line(-1_275)])!;
        Assert.Equal(0m, result.Charge);
        Assert.Equal(37.82m, result.Credit);
        Assert.Equal(-37.82m, result.Total);
    }

    [Fact]
    public void Summarize_Tax_SumsPerLineTaxes_InDollars()
    {
        // charge line tax 736, credit line tax -377 -> (736 + -377) / 100 = 3.59
        var result = ProrationMapper.Summarize([Line(7_355, taxCents: 736), Line(-3_773, taxCents: -377)])!;
        Assert.Equal(3.59m, result.Tax);
    }

    [Fact]
    public void Summarize_LinesWithoutPerLineTaxes_YieldZeroTax()
    {
        // proration lines carry no per-line taxes -> bucket tax is 0
        var result = ProrationMapper.Summarize([Line(3_582)])!;
        Assert.Equal(0m, result.Tax);
    }

    [Fact]
    public void Summarize_Months_RoundsProratedDaysToNearestMonth_MinimumOne()
    {
        // ~365-day span -> 12 months.
        Assert.Equal(12, ProrationMapper.Summarize(
            [Line(3_582, periodStart: new DateTime(2026, 8, 13), periodEnd: new DateTime(2027, 8, 13))])!.Months);

        // 59-day span rounds to 2 (day-based, not calendar months, which would give 1).
        Assert.Equal(2, ProrationMapper.Summarize(
            [Line(3_582, periodStart: new DateTime(2026, 8, 2), periodEnd: new DateTime(2026, 9, 30))])!.Months);

        // Sub-month span floors to 1, never 0.
        Assert.Equal(1, ProrationMapper.Summarize(
            [Line(3_582, periodStart: new DateTime(2026, 8, 13), periodEnd: new DateTime(2026, 8, 20))])!.Months);

        // No line period -> 0.
        Assert.Equal(0, ProrationMapper.Summarize([Line(3_582)])!.Months);
    }

    [Fact]
    public void Summarize_MultipleTaxesOnOneLine_SumsThem()
    {
        var line = new InvoiceLineItem
        {
            Amount = 7_355,
            Taxes = [new InvoiceLineItemTax { Amount = 500 }, new InvoiceLineItemTax { Amount = 236 }],
        };
        var result = ProrationMapper.Summarize([line])!;
        Assert.Equal(7.36m, result.Tax);
    }
}
