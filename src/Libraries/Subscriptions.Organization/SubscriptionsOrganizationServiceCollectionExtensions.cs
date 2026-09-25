using Bit.Api.AdminConsole.Authorization;
using Bit.Invoicing;
using Bit.Subscriptions.Organization.Handlers;
using Bit.Subscriptions.Organization.Queries;
using Bit.Subscriptions.Organization.Requirements;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bit.Subscriptions.Organization;

/// <summary>Registration entry point for the organization-scoped subscription feature library.</summary>
public static class SubscriptionsOrganizationServiceCollectionExtensions
{
    /// <summary>Registers the organization-scoped subscription services and the Invoicing library they depend on.</summary>
    public static IServiceCollection AddOrganizationSubscriptions(this IServiceCollection services)
    {
        services.AddInvoicing();
        services.AddOrganizationAuthorization();
        services.TryAddScoped<OrganizationSubscriptionEndpointsHandler>();
        services.TryAddScoped<IPreviewOrganizationSubscriptionPurchaseQuery, PreviewOrganizationSubscriptionPurchaseQuery>();
        services.TryAddScoped<PreviewOrganizationSubscriptionPurchaseHandler>();
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IAuthorizationHandler, StandaloneOrganizationOwnerRequirementHandler>());
        return services;
    }
}
