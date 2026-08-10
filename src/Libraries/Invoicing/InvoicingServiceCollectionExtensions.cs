using Microsoft.Extensions.DependencyInjection;

namespace Bit.Invoicing;

/// <summary>Registration entry point for the Invoicing library.</summary>
public static class InvoicingServiceCollectionExtensions
{
    /// <summary>Registers everything <c>Bit.Invoicing</c> needs. Uses TryAdd so feature libraries can call it and still compose.</summary>
    public static IServiceCollection AddInvoicing(this IServiceCollection services)
    {
        return services;
    }
}
