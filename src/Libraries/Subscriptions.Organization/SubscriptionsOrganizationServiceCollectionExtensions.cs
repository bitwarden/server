using Bit.Api.AdminConsole.Authorization;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.PrivilegedControlsSeats;
using Bit.Core.Billing.Organizations.Commands;
using Bit.Core.Billing.Organizations.Helpers;
using Bit.Invoicing;
using Bit.Subscriptions.Organization.Commands.InvoicePreviews;
using Bit.Subscriptions.Organization.Commands.PrivilegedControls;
using Bit.Subscriptions.Organization.Commands.Purchase;
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
        services.AddPrivilegedControlsSeatAutoscaling();
        services.TryAddScoped<GetOrganizationSubscriptionPreviewHandler>();
        services.TryAddScoped<IPreviewOrganizationSubscriptionPurchaseQuery, PreviewOrganizationSubscriptionPurchaseQuery>();
        services.TryAddScoped<PreviewOrganizationSubscriptionPurchaseHandler>();
        services.TryAddScoped<PreviewOrganizationPlanChangeHandler>();
        services.TryAddScoped<PurchasePrivilegedControlsHandler>();
        services.TryAddScoped<IPreviewOrganizationPlanChangeCommand, PreviewOrganizationPlanChangeCommand>();
        services.TryAddScoped<PreviewOrganizationRestartHandler>();
        services.TryAddScoped<IPreviewOrganizationRestartCommand, PreviewOrganizationRestartCommand>();
        services.TryAddScoped<IAddPrivilegedControlsToExistingSubscriptionCommand, AddPrivilegedControlsToExistingSubscriptionCommand>();
        services.TryAddScoped<IChangePrivilegedControlsSeatsCommand, ChangePrivilegedControlsSeatsCommand>();
        services.TryAddScoped<ChangePrivilegedControlsSeatsHandler>();
        services.TryAddScoped<RemovePrivilegedControlsHandler>();
        services.TryAddScoped<IRemovePrivilegedControlsCommand, RemovePrivilegedControlsCommand>();
        services.TryAddScoped<ICreateOrganizationSubscriptionCommand, CreateOrganizationSubscriptionCommand>();
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IAuthorizationHandler, StandaloneOrganizationOwnerRequirementHandler>());
        return services;
    }

    /// <summary>
    /// Registers the Privileged Controls seat reserve, autoscale, and revert commands and the services they use. A
    /// host that resolves <see cref="IReservePrivilegedControlsSeatsCommand"/>,
    /// <see cref="IReservePrivilegedControlsSeatForJoiningMemberCommand"/>,
    /// <see cref="IAutoscalePrivilegedControlsSeatsCommand"/>, or <see cref="IRevertPrivilegedControlsSeatsCommand"/>
    /// without mapping the endpoints calls this.
    /// </summary>
    public static IServiceCollection AddPrivilegedControlsSeatAutoscaling(this IServiceCollection services)
    {
        services.TryAddScoped<IPrivilegedControlsSeatChangeSetFactory, PrivilegedControlsSeatChangeSetFactory>();
        services.TryAddScoped<IIsOnPrivilegedControlsSalesInitiatedTrialQuery, IsOnPrivilegedControlsSalesInitiatedTrialQuery>();
        services.TryAddScoped<IAutoscalePrivilegedControlsSeatsCommand, AutoscalePrivilegedControlsSeatsCommand>();
        services.TryAddScoped<IRevertPrivilegedControlsSeatsCommand, RevertPrivilegedControlsSeatsCommand>();
        services.TryAddScoped<IReservePrivilegedControlsSeatsCommand, ReservePrivilegedControlsSeatsCommand>();
        services.TryAddScoped<IReservePrivilegedControlsSeatForJoiningMemberCommand, ReservePrivilegedControlsSeatForJoiningMemberCommand>();
        return services;
    }
}
