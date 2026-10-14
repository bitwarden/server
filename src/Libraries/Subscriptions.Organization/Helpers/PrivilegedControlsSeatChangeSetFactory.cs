using Bit.Core.Billing.Organizations.Helpers;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Pricing;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using OrganizationEntity = Bit.Core.AdminConsole.Entities.Organization;

namespace Bit.Subscriptions.Organization.Helpers;

internal sealed class PrivilegedControlsSeatChangeSetFactory(
    IOrganizationUserRepository organizationUserRepository,
    IPricingClient pricingClient) : IPrivilegedControlsSeatChangeSetFactory
{
    public async Task<PrivilegedControlsSeatChange> CreateAsync(
        OrganizationEntity organization,
        int seats)
    {
        var plan = await pricingClient.GetPlanOrThrow(organization.PlanType);

        if (!plan.SupportsPrivilegedControls)
        {
            throw new BadRequestException("Organization's plan does not support Privileged Controls.");
        }

        if (seats < 1)
        {
            throw new BadRequestException("At least one Privileged Controls seat is required.");
        }

        var minimum = organization.PamSeatMinimum ?? plan.PrivilegedControls.DefaultSeatMinimum;
        var builder = OrganizationSubscriptionChangeSet.Builder(plan);

        var currentSeats = organization.PamSeats.GetValueOrDefault();

        // No seats saved, or none left, means there is no seat line item on the subscription to update.
        if (currentSeats < 1)
        {
            if (seats < minimum)
            {
                throw BelowMinimum(minimum);
            }

            return new PrivilegedControlsSeatChange(builder.AddPrivilegedControlsSeats(seats).Build(), minimum);
        }

        if (seats == currentSeats)
        {
            throw new BadRequestException($"Your organization already has {currentSeats} Privileged Controls seats.");
        }

        if (seats < currentSeats)
        {
            if (seats < minimum)
            {
                throw BelowMinimum(minimum);
            }

            var occupiedSeats = await organizationUserRepository.GetOccupiedPamSeatCountByOrganizationIdAsync(organization.Id);

            if (occupiedSeats > seats)
            {
                throw new BadRequestException(
                    $"{occupiedSeats} users are currently occupying Privileged Controls seats. " +
                    "You cannot decrease your subscription below your current occupied seat count.");
            }
        }

        return new PrivilegedControlsSeatChange(builder.UpdatePrivilegedControlsSeats(seats).Build(), minimum);
    }

    private static BadRequestException BelowMinimum(int minimum) =>
        new($"Privileged Controls requires at least {minimum} seats.");
}
