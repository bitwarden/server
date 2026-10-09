using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Organizations.Commands;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using OrganizationEntity = Bit.Core.AdminConsole.Entities.Organization;

namespace Bit.Subscriptions.Organization.Commands;

internal interface IAddPrivilegedControlsCommand
{
    /// <summary>
    /// Handles an organization's first Privileged Controls purchase: buys the initial seats, sets the autoscale
    /// limit, turns on Privileged Controls, and saves the plan's default seat minimum when the organization has
    /// none yet. The customer is charged immediately.
    /// </summary>
    /// <param name="organization">The organization making its first purchase.</param>
    /// <param name="seats">The number of Privileged Controls seats to buy.</param>
    /// <param name="maxAutoscaleSeats">The most seats the organization can autoscale to, or null for no limit.</param>
    /// <exception cref="BadRequestException">Thrown when the purchase is invalid or rejected.</exception>
    /// <exception cref="ConflictException">Thrown when the organization's subscription is in a state that prevents the purchase.</exception>
    Task Run(OrganizationEntity organization, int seats, int? maxAutoscaleSeats);
}

internal sealed class AddPrivilegedControlsCommand(
    IPrivilegedControlsSeatChangeSetFactory seatChangeSetFactory,
    IUpdateOrganizationSubscriptionCommand updateOrganizationSubscriptionCommand,
    IOrganizationService organizationService) : IAddPrivilegedControlsCommand
{
    public async Task Run(OrganizationEntity organization, int seats, int? maxAutoscaleSeats)
    {
        if (organization.UsePam || organization.PamSeats.GetValueOrDefault() > 0)
        {
            throw new BadRequestException("Your organization already has Privileged Controls.");
        }

        if (maxAutoscaleSeats < seats)
        {
            throw new BadRequestException("Cannot set max seat autoscaling below the Privileged Controls seat count.");
        }

        var seatChange = Unwrap(await seatChangeSetFactory.CreateAsync(organization, seats));
        Unwrap(await updateOrganizationSubscriptionCommand.Run(organization, seatChange.ChangeSet));

        organization.PamSeats = seats;
        organization.MaxAutoscalePamSeats = maxAutoscaleSeats;
        organization.UsePam = true;
        organization.PamSeatMinimum = seatChange.SeatMinimum;
        await organizationService.ReplaceAndUpdateCacheAsync(organization);
    }

    // The seat change step and the subscription update still report failures as a BillingCommandResult. The
    // endpoint exception filter has no case for BillingException, so surface each failure as the exception that
    // maps to its HTTP status.
    private static T Unwrap<T>(BillingCommandResult<T> result) => result.Match(
        value => value,
        badRequest => throw new BadRequestException(badRequest.Response),
        conflict => throw new ConflictException(conflict.Response),
        unhandled => throw unhandled.Exception ?? new InvalidOperationException(unhandled.Response));
}
