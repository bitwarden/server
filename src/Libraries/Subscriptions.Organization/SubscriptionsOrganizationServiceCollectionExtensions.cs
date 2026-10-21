using Bit.Api.AdminConsole.Authorization;
using Bit.Core.Billing.Organizations.Commands;
using Bit.Core.Billing.Organizations.Helpers;
using Bit.Invoicing;
using Bit.Subscriptions.Organization.Commands;
using Bit.Subscriptions.Organization.Handlers;
using Bit.Subscriptions.Organization.Helpers;
using Bit.Subscriptions.Organization.Queries;
using Bit.Subscriptions.Organization.Requirements;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bit.Subscriptions.Organization;

/// <summary>Registration entry point for the organization subscription feature library.</summary>
public static class SubscriptionsOrganizationServiceCollectionExtensions
{
    /// <summary>Registers the organization subscription services and the Invoicing library they depend on.</summary>
    public static IServiceCollection AddOrganizationSubscriptions(this IServiceCollection services)
    {
        services.AddInvoicing();
        services.AddOrganizationAuthorization();
        services.TryAddScoped<GetOrganizationSubscriptionPreviewHandler>();
        services.TryAddScoped<IPreviewOrganizationSubscriptionPurchaseQuery, PreviewOrganizationSubscriptionPurchaseQuery>();
        services.TryAddScoped<PreviewOrganizationSubscriptionPurchaseHandler>();
        services.TryAddScoped<PreviewOrganizationPlanChangeHandler>();
        services.TryAddScoped<PurchasePrivilegedControlsHandler>();
        services.TryAddScoped<IPreviewOrganizationPlanChangeCommand, PreviewOrganizationPlanChangeCommand>();
        services.TryAddScoped<PreviewOrganizationRestartHandler>();
        services.TryAddScoped<IPreviewOrganizationRestartCommand, PreviewOrganizationRestartCommand>();
        services.TryAddScoped<IPrivilegedControlsSeatChangeSetFactory, PrivilegedControlsSeatChangeSetFactory>();
        services.TryAddScoped<IAddPrivilegedControlsToExistingSubscriptionCommand, AddPrivilegedControlsToExistingSubscriptionCommand>();
        services.TryAddScoped<IChangePrivilegedControlsSeatsCommand, ChangePrivilegedControlsSeatsCommand>();
        services.TryAddScoped<IRemovePrivilegedControlsCommand, RemovePrivilegedControlsCommand>();
        services.TryAddScoped<IAutoscalePrivilegedControlsSeatsCommand, AutoscalePrivilegedControlsSeatsCommand>();
        services.TryAddScoped<ICreateOrganizationSubscriptionCommand, CreateOrganizationSubscriptionCommand>();
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IAuthorizationHandler, StandaloneOrganizationOwnerRequirementHandler>());
        return services;
    }
}
