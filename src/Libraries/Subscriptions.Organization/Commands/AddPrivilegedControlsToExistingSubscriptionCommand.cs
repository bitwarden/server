using Bit.Core.Billing.Organizations.Commands;
using Bit.Core.Billing.Organizations.Helpers;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bitwarden.Server.Sdk.Environment;
using OrganizationEntity = Bit.Core.AdminConsole.Entities.Organization;

namespace Bit.Subscriptions.Organization.Commands;

internal interface IAddPrivilegedControlsToExistingSubscriptionCommand
{
    /// <summary>
    /// Handles an organization's first Privileged Controls purchase: buys the initial seats, sets the autoscale
    /// limit, and turns on Privileged Controls. The customer is charged immediately.
    /// </summary>
    /// <param name="organization">The organization making its first purchase.</param>
    /// <param name="seats">The number of Privileged Controls seats to buy.</param>
    /// <param name="maxAutoscaleSeats">The most seats the organization can autoscale to, or null for no limit.</param>
    /// <exception cref="BadRequestException">Thrown when the purchase is invalid or rejected.</exception>
    /// <exception cref="ConflictException">Thrown when the organization's subscription is in a state that prevents the purchase.</exception>
    Task Run(OrganizationEntity organization, int seats, int? maxAutoscaleSeats);
}

internal sealed class AddPrivilegedControlsToExistingSubscriptionCommand(
    IPrivilegedControlsSeatChangeSetFactory seatChangeSetFactory,
    IUpdateOrganizationSubscriptionCommand updateOrganizationSubscriptionCommand,
    IOrganizationService organizationService,
    IBitwardenEnvironment environment) : IAddPrivilegedControlsToExistingSubscriptionCommand
{
    public async Task Run(OrganizationEntity organization, int seats, int? maxAutoscaleSeats)
    {
        if (environment.SelfHosted)
        {
            throw new BadRequestException("Cannot update subscription on a self-hosted instance.");
        }

        if (organization.UsePam || organization.PamSeats.GetValueOrDefault() > 0)
        {
            throw new BadRequestException("Your organization already has Privileged Controls.");
        }

        if (maxAutoscaleSeats < seats)
        {
            throw new BadRequestException("Cannot set max seat autoscaling below the Privileged Controls seat count.");
        }

        var changeSet = await seatChangeSetFactory.CreateAsync(organization, seats);
        (await updateOrganizationSubscriptionCommand.Run(organization, changeSet)).GetValueOrThrowHttpException();

        organization.PamSeats = seats;
        organization.MaxAutoscalePamSeats = maxAutoscaleSeats;
        organization.UsePam = true;
        await organizationService.ReplaceAndUpdateCacheAsync(organization);
    }
}
