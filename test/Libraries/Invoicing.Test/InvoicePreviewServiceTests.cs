using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Exceptions;
using Bit.Invoicing.InvoicePreviews;
using Bit.Invoicing.InvoicePreviews.Models;
using Bit.Invoicing.InvoicePreviews.Stripe;
using Stripe;
using Xunit;

namespace Bit.Invoicing.Test;

public class InvoicePreviewServiceTests
{
    [Fact]
    public async Task GetInvoicePreviewAsync_FromOptions_FetchesThroughClientThenProjects()
    {
        var client = new FakeInvoicePreviewClient(StripeFixtures.SampleInvoiceWithPmSeat());
        var service = new InvoicePreviewService(client, new InvoicePreviewBuilder(new RecordingLogger<InvoicePreviewBuilder>()));

        var preview = await service.GetInvoicePreviewAsync(new InvoiceCreatePreviewOptions(), PlanTierType.Enterprise, PlanCadenceType.Annually);

        Assert.Equal("pm-seat", preview.PasswordManager.Seats.Reference);
        Assert.Equal(1, client.CallCount);
    }

    [Fact]
    public async Task GetInvoicePreviewAsync_FromSubscription_ProjectsWithoutFetching()
    {
        var client = new FakeInvoicePreviewClient(new Invoice());
        var service = new InvoicePreviewService(client, new InvoicePreviewBuilder(new RecordingLogger<InvoicePreviewBuilder>()));

        var preview = await service.GetInvoicePreviewAsync(StripeFixtures.SampleSubscriptionWithPmSeat(), PlanTierType.Premium, PlanCadenceType.Annually);

        Assert.Equal(0m, preview.EstimatedTax);
        Assert.Equal(0, client.CallCount);
    }

    [Fact]
    public async Task GetInvoicePreviewForBillingAddressAsync_TaxComplete_ReturnsPreview()
    {
        var invoice = StripeFixtures.SampleInvoiceWithPmSeat();
        invoice.AutomaticTax = new InvoiceAutomaticTax { Status = "complete" };
        var client = new FakeInvoicePreviewClient(invoice);
        var service = new InvoicePreviewService(client, new InvoicePreviewBuilder(new RecordingLogger<InvoicePreviewBuilder>()));

        var preview = await service.GetInvoicePreviewForBillingAddressAsync(new InvoiceCreatePreviewOptions(), PlanTierType.Enterprise, PlanCadenceType.Annually);

        Assert.Equal("pm-seat", preview.PasswordManager.Seats.Reference);
        Assert.Equal(1, client.CallCount);
    }

    [Fact]
    public async Task GetInvoicePreviewForBillingAddressAsync_TaxCompleteWithNoTaxLines_ReturnsZeroTaxPreview()
    {
        var invoice = StripeFixtures.SampleInvoiceWithPmSeat();
        invoice.AutomaticTax = new InvoiceAutomaticTax { Status = "complete" };
        invoice.TotalTaxes = null;
        var service = new InvoicePreviewService(new FakeInvoicePreviewClient(invoice), new InvoicePreviewBuilder(new RecordingLogger<InvoicePreviewBuilder>()));

        var preview = await service.GetInvoicePreviewForBillingAddressAsync(new InvoiceCreatePreviewOptions(), PlanTierType.Enterprise, PlanCadenceType.Annually);

        Assert.Equal(0m, preview.EstimatedTax);
    }

    [Theory]
    [InlineData("requires_location_inputs")]
    [InlineData("failed")]
    public async Task GetInvoicePreviewForBillingAddressAsync_TaxNotComplete_ThrowsKeyedBadRequest(string status)
    {
        var invoice = StripeFixtures.SampleInvoiceWithPmSeat();
        invoice.AutomaticTax = new InvoiceAutomaticTax { Status = status };
        var service = new InvoicePreviewService(new FakeInvoicePreviewClient(invoice), new InvoicePreviewBuilder(new RecordingLogger<InvoicePreviewBuilder>()));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            service.GetInvoicePreviewForBillingAddressAsync(new InvoiceCreatePreviewOptions(), PlanTierType.Enterprise, PlanCadenceType.Annually));

        Assert.Equal(
            "Your location wasn't recognized. Please ensure your country and postal code are valid and try again.",
            exception.ModelState![BillingErrorKeys.BillingAddress]!.Errors.Single().ErrorMessage);
    }

    [Fact]
    public async Task GetInvoicePreviewForBillingAddressAsync_NoAutomaticTax_ThrowsKeyedBadRequest()
    {
        var invoice = StripeFixtures.SampleInvoiceWithPmSeat();
        invoice.AutomaticTax = null;
        var service = new InvoicePreviewService(new FakeInvoicePreviewClient(invoice), new InvoicePreviewBuilder(new RecordingLogger<InvoicePreviewBuilder>()));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            service.GetInvoicePreviewForBillingAddressAsync(new InvoiceCreatePreviewOptions(), PlanTierType.Enterprise, PlanCadenceType.Annually));

        Assert.Equal(
            "Your location wasn't recognized. Please ensure your country and postal code are valid and try again.",
            exception.ModelState![BillingErrorKeys.BillingAddress]!.Errors.Single().ErrorMessage);
    }

    // IInvoicePreviewClient is internal, so it is hand-faked via Invoicing.Test's InternalsVisibleTo grant rather than mocked — NSubstitute's proxy assembly cannot see it. Same reasoning as RecordingLogger.
    private sealed class FakeInvoicePreviewClient(Invoice invoice) : IInvoicePreviewClient
    {
        public int CallCount { get; private set; }

        public Task<Invoice> GetInvoiceForPreviewAsync(InvoiceCreatePreviewOptions options)
        {
            CallCount++;
            return Task.FromResult(invoice);
        }
    }
}
