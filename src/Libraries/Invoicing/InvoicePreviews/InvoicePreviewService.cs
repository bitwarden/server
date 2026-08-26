using Bit.Core.Billing.Enums;
using Bit.Invoicing.InvoicePreviews.Models;
using Bit.Invoicing.InvoicePreviews.Stripe;
using Stripe;

namespace Bit.Invoicing.InvoicePreviews;

public interface IInvoicePreviewService
{
    /// <summary>Fetches an upcoming Stripe invoice for the given options and projects it into an <see cref="InvoicePreview"/>.</summary>
    Task<InvoicePreview> GetInvoicePreviewAsync(InvoiceCreatePreviewOptions options, PlanTierType planTier, PlanCadenceType cadence);

    /// <summary>Projects a subscription's current items when it has no upcoming invoice (canceled or suspended).</summary>
    Task<InvoicePreview> GetInvoicePreviewAsync(Subscription subscription, PlanTierType planTier, PlanCadenceType cadence);
}

internal sealed class InvoicePreviewService(IInvoicePreviewClient client, InvoicePreviewBuilder builder) : IInvoicePreviewService
{
    public async Task<InvoicePreview> GetInvoicePreviewAsync(InvoiceCreatePreviewOptions options, PlanTierType planTier, PlanCadenceType cadence)
    {
        var invoice = await client.GetInvoiceForPreviewAsync(options);
        return builder.Build(invoice, planTier, cadence);
    }

    public Task<InvoicePreview> GetInvoicePreviewAsync(Subscription subscription, PlanTierType planTier, PlanCadenceType cadence) =>
        Task.FromResult(builder.Build(subscription, planTier, cadence));
}
