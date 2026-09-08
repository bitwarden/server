using Bit.Invoicing;
using Bit.Subscriptions.User.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bit.Subscriptions.User;

/// <summary>Registration entry point for the account-scoped subscription feature library.</summary>
public static class SubscriptionsUserServiceCollectionExtensions
{
    /// <summary>Registers the account-scoped subscription services and the Invoicing library they depend on.</summary>
    public static IServiceCollection AddUserSubscriptions(this IServiceCollection services)
    {
        services.AddInvoicing();
        services.TryAddScoped<UserSubscriptionEndpointsHandler>();
        return services;
    }
}
