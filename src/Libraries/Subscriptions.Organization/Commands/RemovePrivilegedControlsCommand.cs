using Bit.Core.Billing.Organizations.Commands;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Pricing;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bitwarden.Server.Sdk.Environment;
using OrganizationEntity = Bit.Core.AdminConsole.Entities.Organization;

namespace Bit.Subscriptions.Organization.Commands;

internal interface IRemovePrivilegedControlsCommand
{
    /// <summary>
    /// Removes Privileged Controls from an organization: turns the entitlement off for every user, removes the
    /// seat line item from the subscription with an immediate credit, and clears the seat count and autoscale
    /// limit. The seat minimum and each user's individual access flag are left as they are.
    /// </summary>
    /// <param name="organization">The organization that currently has Privileged Controls.</param>
    /// <exception cref="BadRequestException">Thrown when the removal is invalid or rejected.</exception>
    /// <exception cref="ConflictException">Thrown when the organization's subscription is in a state that prevents the removal.</exception>
    Task Run(OrganizationEntity organization);
}

internal sealed class RemovePrivilegedControlsCommand(
    IPricingClient pricingClient,
    IUpdateOrganizationSubscriptionCommand updateOrganizationSubscriptionCommand,
    IOrganizationService organizationService,
    IBitwardenEnvironment environment) : IRemovePrivilegedControlsCommand
{
    public async Task Run(OrganizationEntity organization)
    {
        if (environment.SelfHosted)
        {
            throw new BadRequestException("Cannot update subscription on a self-hosted instance.");
        }

        if (!organization.UsePam || organization.PamSeats.GetValueOrDefault() < 1)
        {
            throw new BadRequestException("Your organization doesn't have Privileged Controls.");
        }

        if (string.IsNullOrWhiteSpace(organization.GatewaySubscriptionId))
        {
            throw new BadRequestException("No subscription found.");
        }

        var plan = await pricingClient.GetPlanOrThrow(organization.PlanType);
        var changeSet = OrganizationSubscriptionChangeSet.Builder(plan)
            .RemovePrivilegedControlsSeats()
            .Build();
        (await updateOrganizationSubscriptionCommand.Run(organization, changeSet)).GetValueOrThrowHttpException();

        organization.UsePam = false;
        organization.PamSeats = null;
        organization.MaxAutoscalePamSeats = null;
        await organizationService.ReplaceAndUpdateCacheAsync(organization);
    }
}
