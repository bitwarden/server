using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Exceptions;
using Bit.Invoicing.InvoicePreviews.Models;
using Bit.Invoicing.InvoicePreviews.Stripe;
using Stripe;

namespace Bit.Invoicing.InvoicePreviews;

public interface IInvoicePreviewService
{
    /// <summary>Fetches an upcoming Stripe invoice for the given options and projects it into an <see cref="InvoicePreview"/>.</summary>
    Task<InvoicePreview> GetInvoicePreviewAsync(InvoiceCreatePreviewOptions options, PlanTierType planTier, PlanCadenceType cadence);

    /// <summary>Like <see cref="GetInvoicePreviewAsync(InvoiceCreatePreviewOptions, PlanTierType, PlanCadenceType)"/>, but throws a <see cref="BadRequestException"/> keyed to the billing address when Stripe could not calculate tax.</summary>
    Task<InvoicePreview> GetInvoicePreviewForBillingAddressAsync(InvoiceCreatePreviewOptions options, PlanTierType planTier, PlanCadenceType cadence);

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

    public async Task<InvoicePreview> GetInvoicePreviewForBillingAddressAsync(InvoiceCreatePreviewOptions options, PlanTierType planTier, PlanCadenceType cadence)
    {
        var invoice = await client.GetInvoiceForPreviewAsync(options);

        // Stripe reports an unlocatable address through the invoice's tax status rather than as an error.
        if (invoice.AutomaticTax?.Status != StripeConstants.InvoiceAutomaticTaxStatus.Complete)
        {
            throw new BadRequestException(
                BillingErrorKeys.BillingAddress,
                "Your location wasn't recognized. Please ensure your country and postal code are valid and try again.");
        }

        return builder.Build(invoice, planTier, cadence);
    }

    public Task<InvoicePreview> GetInvoicePreviewAsync(Subscription subscription, PlanTierType planTier, PlanCadenceType cadence) =>
        Task.FromResult(builder.Build(subscription, planTier, cadence));
}
