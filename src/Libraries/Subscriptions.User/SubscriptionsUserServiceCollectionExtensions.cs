using Bit.HttpExtensions;
using Bit.Invoicing;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Subscriptions.User;

/// <summary>Registration entry point for the account-scoped subscription feature library.</summary>
public static class SubscriptionsUserServiceCollectionExtensions
{
    /// <summary>Registers the account-scoped subscription services and the Invoicing library they depend on. Safe to call alongside AddInvoicing (both use TryAdd).</summary>
    public static IServiceCollection AddUserSubscriptions(this IServiceCollection services)
    {
        services.AddInvoicing();
        services.AddUserSubscriptionsOpenApiEndpointDataSource();
        return services;
    }

    private static IServiceCollection AddUserSubscriptionsOpenApiEndpointDataSource(this IServiceCollection services)
        => services.AddOpenApiEndpointDataSource(endpoints => endpoints.MapUserSubscriptionEndpoints());
}
