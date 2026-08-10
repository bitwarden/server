using Bit.Invoicing.InvoicePreviews;
using Bit.Invoicing.InvoicePreviews.Stripe;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bit.Invoicing;

/// <summary>Registration entry point for the Invoicing library.</summary>
public static class InvoicingServiceCollectionExtensions
{
    /// <summary>Registers everything <c>Bit.Invoicing</c> needs, including its owned feature flag keys.</summary>
    public static IServiceCollection AddInvoicing(this IServiceCollection services)
    {
        services.TryAddSingleton<IInvoicePreviewService, InvoicePreviewService>();
        services.TryAddSingleton<IInvoicePreviewClient, InvoicePreviewClient>();
        services.TryAddSingleton<InvoicePreviewBuilder>();
        services.AddKnownFeatureFlags(InvoicingFeatureFlags.GetKeys());
        return services;
    }
}
