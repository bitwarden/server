using Bit.Invoicing;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Subscriptions.Organization;

/// <summary>Registration entry point for the organization-scoped subscription feature library.</summary>
public static class SubscriptionsOrganizationServiceCollectionExtensions
{
    /// <summary>Registers the organization-scoped subscription services and the Invoicing library they depend on. Safe to call alongside AddInvoicing (both use TryAdd).</summary>
    public static IServiceCollection AddOrganizationSubscriptions(this IServiceCollection services)
    {
        services.AddInvoicing();
        return services;
    }
}
