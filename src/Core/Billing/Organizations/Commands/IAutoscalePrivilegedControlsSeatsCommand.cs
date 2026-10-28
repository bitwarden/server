using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Exceptions;
using OrganizationEntity = Bit.Core.AdminConsole.Entities.Organization;

namespace Bit.Core.Billing.Organizations.Commands;

/// <summary>
/// Makes sure an organization has enough Privileged Controls seats for users who are about to be granted access,
/// buying more seats up to the organization's autoscale limit. It validates and updates the subscription only. The
/// caller grants the access.
/// </summary>
public interface IAutoscalePrivilegedControlsSeatsCommand
{
    /// <summary>
    /// Adds Privileged Controls seats when the seats in use plus <paramref name="seatsToAdd"/> exceed the seats the
    /// organization has bought. Nothing changes when the bought seats already cover that total. When a limit is set
    /// and the total equals it, the organization's Owners are emailed.
    /// </summary>
    /// <param name="organization">The organization that already has Privileged Controls.</param>
    /// <param name="seatsToAdd">
    /// The number of users about to be granted Privileged Controls access who do not already occupy a seat.
    /// </param>
    /// <returns>
    /// A successful result, or a failed result carrying the autoscale limit when the total would pass it.
    /// </returns>
    /// <exception cref="BadRequestException">
    /// Thrown on a self-hosted instance, when <paramref name="seatsToAdd"/> is below 1, when the organization does
    /// not have Privileged Controls or has no subscription, or when the seat change or
    /// subscription update is rejected.
    /// </exception>
    /// <exception cref="ConflictException">Thrown when the organization's subscription is in a state that prevents the change.</exception>
    Task<AutoscalePrivilegedControlsSeatsResult> Run(OrganizationEntity organization, int seatsToAdd);
}
