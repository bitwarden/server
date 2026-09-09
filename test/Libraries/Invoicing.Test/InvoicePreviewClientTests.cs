using Bit.Core.Billing.Services;
using Bit.Invoicing.InvoicePreviews.Stripe;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
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

    [Fact]
    public async Task GetInvoiceForPreviewAsync_RefetchesDistinctCouponsWithAppliesToAndSplices()
    {
        var adapter = Substitute.For<IStripeAdapter>();
        adapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>())
            .Returns(Invoice.FromJson("""
            {
              "id": "in_test",
              "total_discount_amounts": [
                { "amount": 225, "discount": { "id": "di_1", "source": { "coupon": { "id": "cp_a", "name": "A", "percent_off": 15 } } } },
                { "amount": 744, "discount": { "id": "di_2", "source": { "coupon": { "id": "cp_a", "name": "A", "percent_off": 15 } } } },
                { "amount": 100, "discount": { "id": "di_3", "source": { "coupon": { "id": "cp_b", "name": "B", "percent_off": 10 } } } }
              ],
              "lines": { "data": [] }
            }
            """));
        var enriched = new Coupon
        {
            Id = "cp_a",
            Name = "A",
            PercentOff = 15,
            AppliesTo = new CouponAppliesTo { Products = ["prod_storage"] },
        };
        adapter.GetCouponAsync("cp_a", Arg.Any<CouponGetOptions>()).Returns(enriched);
        adapter.GetCouponAsync("cp_b", Arg.Any<CouponGetOptions>()).Returns(new Coupon { Id = "cp_b", Name = "B" });

        var client = new InvoicePreviewClient(adapter);

        var result = await client.GetInvoiceForPreviewAsync(new InvoiceCreatePreviewOptions());

        await adapter.Received(1).GetCouponAsync("cp_a", Arg.Is<CouponGetOptions>(o => o.Expand.Contains("applies_to")));
        await adapter.Received(1).GetCouponAsync("cp_b", Arg.Any<CouponGetOptions>());
        Assert.Same(enriched, result.TotalDiscountAmounts[0].Discount.Source.Coupon);
        Assert.Same(enriched, result.TotalDiscountAmounts[1].Discount.Source.Coupon);
        Assert.Null(result.TotalDiscountAmounts[2].Discount.Source.Coupon.AppliesTo);
    }

    [Fact]
    public async Task GetInvoiceForPreviewAsync_CouponDeleted_KeepsUnenrichedCoupon()
    {
        var adapter = Substitute.For<IStripeAdapter>();
        adapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>())
            .Returns(Invoice.FromJson("""
            {
              "id": "in_test",
              "total_discount_amounts": [
                { "amount": 225, "discount": { "id": "di_1", "source": { "coupon": { "id": "cp_gone", "name": "GONE", "percent_off": 15 } } } }
              ],
              "lines": { "data": [] }
            }
            """));
        adapter.GetCouponAsync("cp_gone", Arg.Any<CouponGetOptions>())
            .Throws(new StripeException(System.Net.HttpStatusCode.BadRequest, new StripeError { Code = "resource_missing" }, "gone"));

        var client = new InvoicePreviewClient(adapter);

        var result = await client.GetInvoiceForPreviewAsync(new InvoiceCreatePreviewOptions());

        var coupon = result.TotalDiscountAmounts[0].Discount.Source.Coupon;
        Assert.Equal("cp_gone", coupon.Id);
        Assert.Null(coupon.AppliesTo);
    }

    [Fact]
    public async Task GetInvoiceForPreviewAsync_NoDiscounts_DoesNotRefetchCoupons()
    {
        var adapter = Substitute.For<IStripeAdapter>();
        adapter.CreateInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>())
            .Returns(Invoice.FromJson("""
            {
              "id": "in_test",
              "lines": { "data": [ { "id": "il_1" } ] }
            }
            """));

        var client = new InvoicePreviewClient(adapter);

        await client.GetInvoiceForPreviewAsync(new InvoiceCreatePreviewOptions());

        await adapter.DidNotReceive().GetCouponAsync(Arg.Any<string>(), Arg.Any<CouponGetOptions>());
    }
}
