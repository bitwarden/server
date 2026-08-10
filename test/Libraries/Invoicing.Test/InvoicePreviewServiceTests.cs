using Bit.Core.Billing.Enums;
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
