using Bit.Invoicing.InvoicePreviews;
using Bit.Invoicing.InvoicePreviews.Queries;
using Bit.Invoicing.InvoicePreviews.Stripe;
using Bitwarden.Server.Sdk.Environment;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bit.Invoicing;

/// <summary>Registration entry point for the Invoicing library.</summary>
public static class InvoicingServiceCollectionExtensions
{
    /// <summary>Registers everything <c>Bit.Invoicing</c> needs, including its owned feature flag keys. Resolving <see cref="IInvoicePreviewService"/> in a self-hosted environment throws — the library is cloud-only.</summary>
    public static IServiceCollection AddInvoicing(this IServiceCollection services)
    {
        services.TryAddSingleton<InvoicePreviewService>();
        services.TryAddSingleton<IInvoicePreviewClient, InvoicePreviewClient>();
        services.TryAddSingleton<InvoicePreviewBuilder>();
        services.TryAddSingleton<IInvoicePreviewService>(sp =>
        {
            if (sp.GetRequiredService<IBitwardenEnvironment>().SelfHosted)
            {
                throw new InvalidOperationException(
                    "Bit.Invoicing must never be resolved in a self-hosted environment.");
            }

            return sp.GetRequiredService<InvoicePreviewService>();
        });
        services.AddKnownFeatureFlags(InvoicingFeatureFlags.GetKeys());
        services.TryAddScoped<IGetSubscriptionPreviewQuery, GetSubscriptionPreviewQuery>();
        return services;
    }
}
