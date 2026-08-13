using Bit.Invoicing.InvoicePreviews;
using Stripe;
using Xunit;

namespace Bit.Invoicing.Test;

public class ProrationMapperTests
{
    private static InvoiceLineItem Line(long amountCents, DateTime? periodEnd = null, long taxCents = 0) => new()
    {
        Amount = amountCents,
        Period = periodEnd is null ? null : new InvoiceLineItemPeriod { End = periodEnd.Value },
        Taxes = taxCents == 0 ? null : [new InvoiceLineItemTax { Amount = taxCents }],
    };

    private static Invoice InvoiceWith(long totalCents, long totalTaxCents, DateTime periodEnd) => new()
    {
        Total = totalCents,
        TotalTaxes = [new InvoiceTotalTax { Amount = totalTaxCents }],
        PeriodEnd = periodEnd,
    };

    [Fact]
    public void Summarize_EmptyBucket_ReturnsNull()
        => Assert.Null(ProrationMapper.Summarize([], InvoiceWith(0, 0, new DateTime(2027, 1, 1))));

    [Fact]
    public void Summarize_SplitsChargeCreditAndNet_InDollars()
    {
        var invoice = InvoiceWith(totalCents: 11_982, totalTaxCents: 0, periodEnd: new DateTime(2027, 1, 1));
        var result = ProrationMapper.Summarize([Line(7_355), Line(-3_773)], invoice)!;
        Assert.Equal(73.55m, result.Charge);
        Assert.Equal(37.73m, result.Credit);
        Assert.Equal(35.82m, result.Total);
    }

    [Fact]
    public void Summarize_EntirelyNegativeBucket_IsAllCredit()
    {
        var invoice = InvoiceWith(11_982, 0, new DateTime(2027, 1, 1));
        var result = ProrationMapper.Summarize([Line(-2_507), Line(-1_275)], invoice)!;
        Assert.Equal(0m, result.Charge);
        Assert.Equal(37.82m, result.Credit);
        Assert.Equal(-37.82m, result.Total);
    }

    [Fact]
    public void Summarize_Tax_SumsPerLineTaxes_InDollars()
    {
        // charge line tax 736, credit line tax -377 -> (736 + -377) / 100 = 3.59
        var invoice = InvoiceWith(totalCents: 11_982, totalTaxCents: 0, periodEnd: new DateTime(2027, 1, 1));
        var result = ProrationMapper.Summarize([Line(7_355, taxCents: 736), Line(-3_773, taxCents: -377)], invoice)!;
        Assert.Equal(3.59m, result.Tax);
    }

    [Fact]
    public void Summarize_LinesWithoutPerLineTaxes_YieldZeroTax()
    {
        // invoice carries tax, but the proration lines have none -> bucket tax is 0 (no allocation from the invoice total)
        var invoice = InvoiceWith(totalCents: 11_982, totalTaxCents: 1_207, periodEnd: new DateTime(2027, 1, 1));
        var result = ProrationMapper.Summarize([Line(3_582)], invoice)!;
        Assert.Equal(0m, result.Tax);
    }

    [Fact]
    public void Summarize_Months_RoundsProratedDaysToNearestMonth_MinimumOne()
    {
        // Real proration shape: line end is LATER than invoice end. ~365 days -> 12 months.
        var yearOut = InvoiceWith(3_582, 0, periodEnd: new DateTime(2026, 8, 13));
        Assert.Equal(12, ProrationMapper.Summarize([Line(3_582, periodEnd: new DateTime(2027, 8, 13))], yearOut)!.Months);

        // 59-day gap rounds to 2 (day-based, not calendar months, which would give 1).
        var partial = InvoiceWith(3_582, 0, periodEnd: new DateTime(2026, 8, 2));
        Assert.Equal(2, ProrationMapper.Summarize([Line(3_582, periodEnd: new DateTime(2026, 9, 30))], partial)!.Months);

        // Sub-month gap floors to 1, never 0.
        var tiny = InvoiceWith(3_582, 0, periodEnd: new DateTime(2026, 8, 13));
        Assert.Equal(1, ProrationMapper.Summarize([Line(3_582, periodEnd: new DateTime(2026, 8, 20))], tiny)!.Months);

        // No line period -> 0.
        Assert.Equal(0, ProrationMapper.Summarize([Line(3_582)], tiny)!.Months);
    }

    [Fact]
    public void Summarize_MultipleTaxesOnOneLine_SumsThem()
    {
        var invoice = InvoiceWith(totalCents: 11_982, totalTaxCents: 0, periodEnd: new DateTime(2027, 1, 1));
        var line = new InvoiceLineItem
        {
            Amount = 7_355,
            Taxes = [new InvoiceLineItemTax { Amount = 500 }, new InvoiceLineItemTax { Amount = 236 }],
        };
        var result = ProrationMapper.Summarize([line], invoice)!;
        Assert.Equal(7.36m, result.Tax);
    }
}
