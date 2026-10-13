using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Pricing;
using Bit.Core.Repositories;

namespace Bit.Core.Billing.Organizations.Models;

/// <summary>
/// Validates a requested Privileged Controls seat count against the organization's minimum and the
/// seats in use, then builds the change set that applies it. Every Privileged Controls seat change
/// goes through this type, and a request it rejects returns a bad request result.
/// </summary>
public interface IPrivilegedControlsSeatChangeSetFactory
{
    /// <summary>
    /// Builds the change set that moves the organization to <paramref name="seats"/> Privileged Controls
    /// seats. Does not call Stripe or save anything: the caller applies the change set through
    /// <see cref="Commands.IUpdateOrganizationSubscriptionCommand"/> and saves
    /// <see cref="Organization.PamSeats"/>.
    /// </summary>
    /// <param name="organization">The organization whose seats are changing.</param>
    /// <param name="seats">The total number of Privileged Controls seats being requested.</param>
    /// <returns>
    /// The change set on success, or a <see cref="BadRequest"/> when the request violates a seat rule.
    /// </returns>
    Task<BillingCommandResult<OrganizationSubscriptionChangeSet>> CreateAsync(Organization organization, int seats);
}

public class PrivilegedControlsSeatChangeSetFactory(
    IOrganizationUserRepository organizationUserRepository,
    IPricingClient pricingClient) : IPrivilegedControlsSeatChangeSetFactory
{
    public async Task<BillingCommandResult<OrganizationSubscriptionChangeSet>> CreateAsync(
        Organization organization,
        int seats)
    {
        var plan = await pricingClient.GetPlanOrThrow(organization.PlanType);

        if (!plan.SupportsPrivilegedControls)
        {
            return new BadRequest("Organization's plan does not support Privileged Controls.");
        }

        if (seats < 1)
        {
            return new BadRequest("At least one Privileged Controls seat is required.");
        }

        var minimum = organization.PamSeatMinimum ?? plan.PrivilegedControls.DefaultSeatMinimum;
        var builder = OrganizationSubscriptionChangeSet.Builder(plan);

        var currentSeats = organization.PamSeats.GetValueOrDefault();

        // No seats saved, or none left, means there is no seat line item on the subscription to update.
        if (currentSeats < 1)
        {
            return seats < minimum
                ? BelowMinimum(minimum)
                : builder.AddPrivilegedControlsSeats(seats).Build();
        }

        if (seats == currentSeats)
        {
            return new BadRequest($"Your organization already has {currentSeats} Privileged Controls seats.");
        }

        if (seats < currentSeats)
        {
            if (seats < minimum)
            {
                return BelowMinimum(minimum);
            }

            var occupiedSeats = await organizationUserRepository.GetOccupiedPamSeatCountByOrganizationIdAsync(organization.Id);

            if (occupiedSeats > seats)
            {
                return new BadRequest(
                    $"{occupiedSeats} users are currently occupying Privileged Controls seats. " +
                    "You cannot decrease your subscription below your current occupied seat count.");
            }
        }

        return builder.UpdatePrivilegedControlsSeats(seats).Build();
    }

    private static BadRequest BelowMinimum(int minimum) =>
        new($"Privileged Controls requires at least {minimum} seats.");
}
