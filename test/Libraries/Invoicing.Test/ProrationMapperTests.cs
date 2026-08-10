using Bit.Invoicing.InvoicePreviews;
using Stripe;
using Xunit;

namespace Bit.Invoicing.Test;

public class ProrationMapperTests
{
    private static InvoiceLineItem Line(long amountCents, DateTime? periodEnd = null) => new()
    {
        Amount = amountCents,
        Period = periodEnd is null ? null : new InvoiceLineItemPeriod { End = periodEnd.Value },
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
    public void Summarize_AllocatesTax_ProportionalToNetOverInvoiceTotal()
    {
        // net 3_582 of total 11_982; tax 1_207 -> 1207 * (3582/11982) / 100 = 3.6086...
        var invoice = InvoiceWith(totalCents: 11_982, totalTaxCents: 1_207, periodEnd: new DateTime(2027, 1, 1));
        var result = ProrationMapper.Summarize([Line(7_355), Line(-3_773)], invoice)!;
        Assert.Equal(1207m * (3582m / 11982m) / 100m, result.Tax);
    }

    [Fact]
    public void Summarize_ZeroInvoiceTotal_YieldsZeroTax()
    {
        var invoice = InvoiceWith(totalCents: 0, totalTaxCents: 1_207, periodEnd: new DateTime(2027, 1, 1));
        var result = ProrationMapper.Summarize([Line(3_582)], invoice)!;
        Assert.Equal(0m, result.Tax);
    }

    [Fact]
    public void Summarize_Months_FromLinePeriodEndToInvoicePeriodEnd_FlooredAtZero()
    {
        var invoice = InvoiceWith(3_582, 0, periodEnd: new DateTime(2027, 1, 1));
        Assert.Equal(11, ProrationMapper.Summarize([Line(3_582, periodEnd: new DateTime(2026, 2, 1))], invoice)!.Months);
        // line end after invoice end -> floored to 0
        Assert.Equal(0, ProrationMapper.Summarize([Line(3_582, periodEnd: new DateTime(2027, 6, 1))], invoice)!.Months);
        // no period -> 0
        Assert.Equal(0, ProrationMapper.Summarize([Line(3_582)], invoice)!.Months);
    }
}
