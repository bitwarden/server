using Bit.Core.Billing.Organizations.Commands;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Core.Settings;
using OrganizationEntity = Bit.Core.AdminConsole.Entities.Organization;

namespace Bit.Subscriptions.Organization.Commands;

internal interface IChangePrivilegedControlsSeatsCommand
{
    /// <summary>
    /// Sets an organization's Privileged Controls seat count and autoscale limit. The subscription is only
    /// updated, and prorated, when the seat count changes; a call that changes only the limit saves it without
    /// touching Stripe. The seat minimum is never changed.
    /// </summary>
    /// <param name="organization">The organization that already has Privileged Controls.</param>
    /// <param name="seats">The total number of Privileged Controls seats being requested.</param>
    /// <param name="maxAutoscaleSeats">The most seats the organization can autoscale to, or null for no limit.</param>
    /// <exception cref="BadRequestException">Thrown when the change is invalid or rejected.</exception>
    /// <exception cref="ConflictException">Thrown when the organization's subscription is in a state that prevents the change.</exception>
    Task Run(OrganizationEntity organization, int seats, int? maxAutoscaleSeats);
}

internal sealed class ChangePrivilegedControlsSeatsCommand(
    IPrivilegedControlsSeatChangeSetFactory seatChangeSetFactory,
    IUpdateOrganizationSubscriptionCommand updateOrganizationSubscriptionCommand,
    IOrganizationService organizationService,
    IGlobalSettings globalSettings) : IChangePrivilegedControlsSeatsCommand
{
    public async Task Run(OrganizationEntity organization, int seats, int? maxAutoscaleSeats)
    {
        if (globalSettings.SelfHosted)
        {
            throw new BadRequestException("Cannot update subscription on a self-hosted instance.");
        }

        // Without Privileged Controls the seat change step would treat this as a first purchase and charge immediately.
        if (!organization.UsePam || organization.PamSeats.GetValueOrDefault() < 1)
        {
            throw new BadRequestException("Your organization doesn't have Privileged Controls.");
        }

        if (string.IsNullOrWhiteSpace(organization.GatewayCustomerId))
        {
            throw new BadRequestException("No payment method found.");
        }

        if (string.IsNullOrWhiteSpace(organization.GatewaySubscriptionId))
        {
            throw new BadRequestException("No subscription found.");
        }

        if (maxAutoscaleSeats < seats)
        {
            throw new BadRequestException("Cannot set max seat autoscaling below the Privileged Controls seat count.");
        }

        if (seats != organization.PamSeats)
        {
            var seatChange = (await seatChangeSetFactory.CreateAsync(organization, seats)).GetValueOrThrowHttpException();
            (await updateOrganizationSubscriptionCommand.Run(organization, seatChange.ChangeSet)).GetValueOrThrowHttpException();
        }

        organization.PamSeats = seats;
        organization.MaxAutoscalePamSeats = maxAutoscaleSeats;
        await organizationService.ReplaceAndUpdateCacheAsync(organization);
    }
}
