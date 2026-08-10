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
}
