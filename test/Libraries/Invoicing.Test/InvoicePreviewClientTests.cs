using Bit.Core.Billing.Services;
using Bit.Invoicing.InvoicePreviews.Stripe;
using NSubstitute;
using Stripe;
using Xunit;

namespace Bit.Invoicing.Test;

public class InvoicePreviewClientTests
{
    [Fact]
    public async Task GetInvoiceForPreviewAsync_SetsBothExpandsAndOmitsLineLevelCouponPath()
    {
        var adapter = Substitute.For<IStripeAdapter>();
        adapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(new Invoice());
        var client = new InvoicePreviewClient(adapter);

        var options = new InvoiceCreatePreviewOptions();
        await client.GetInvoiceForPreviewAsync(options);

        Assert.Contains("lines.data.pricing.price_details.price", options.Expand);
        Assert.Contains("total_discount_amounts.discount.source.coupon", options.Expand);
        Assert.DoesNotContain("lines.data.discount_amounts.discount.source.coupon", options.Expand);
    }

    [Fact]
    public async Task GetInvoiceForPreviewAsync_LinesTruncated_FetchesFullSetByInvoiceIdAndSplices()
    {
        var adapter = Substitute.For<IStripeAdapter>();

        var truncated = new Invoice
        {
            Id = "upcoming_in_test",
            Lines = new StripeList<InvoiceLineItem>
            {
                Data = [new InvoiceLineItem { Id = "il_1" }],
                HasMore = true,
            },
        };
        adapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(truncated);

        var fullSet = new List<InvoiceLineItem>
        {
            new() { Id = "il_1" }, new() { Id = "il_2" }, new() { Id = "il_3" },
        };
        adapter.ListInvoiceLineItemsAsync(Arg.Any<string>(), Arg.Any<InvoiceLineItemListOptions>())
            .Returns(fullSet);

        var client = new InvoicePreviewClient(adapter);

        var result = await client.GetInvoiceForPreviewAsync(new InvoiceCreatePreviewOptions());

        // Fetched by the preview's ephemeral id, carrying only the line-level price expand.
        await adapter.Received(1).ListInvoiceLineItemsAsync(
            "upcoming_in_test",
            Arg.Is<InvoiceLineItemListOptions>(o => o.Expand.Contains("data.pricing.price_details.price")));

        Assert.Same(fullSet, result.Lines.Data);
        Assert.Equal(3, result.Lines.Data.Count);
        Assert.False(result.Lines.HasMore);
    }

    [Fact]
    public async Task GetInvoiceForPreviewAsync_LinesNotTruncated_DoesNotFetchAndLeavesLinesUntouched()
    {
        var adapter = Substitute.For<IStripeAdapter>();

        var lines = new List<InvoiceLineItem> { new() { Id = "il_1" } };
        var invoice = new Invoice
        {
            Id = "upcoming_in_test",
            Lines = new StripeList<InvoiceLineItem> { Data = lines, HasMore = false },
        };
        adapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>()).Returns(invoice);

        var client = new InvoicePreviewClient(adapter);

        var result = await client.GetInvoiceForPreviewAsync(new InvoiceCreatePreviewOptions());

        await adapter.DidNotReceive()
            .ListInvoiceLineItemsAsync(Arg.Any<string>(), Arg.Any<InvoiceLineItemListOptions>());
        Assert.Same(lines, result.Lines.Data);
    }
}
