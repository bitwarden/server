using Bit.Core.Billing.Organizations.Commands;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Exceptions;
using OrganizationEntity = Bit.Core.AdminConsole.Entities.Organization;

namespace Bit.Core.Billing.Organizations.Helpers;

/// <summary>
/// Validates a requested Privileged Controls seat count against the organization's Password Manager
/// seats and the seats in use, then builds the change set that applies it. Every Privileged Controls seat change
/// goes through this type, and a request it rejects throws a <see cref="BadRequestException"/>.
/// </summary>
public interface IPrivilegedControlsSeatChangeSetFactory
{
    /// <summary>
    /// Builds the change set that moves the organization to <paramref name="seats"/> Privileged Controls
    /// seats. Does not call Stripe or save anything: the caller applies the change set through
    /// <see cref="IUpdateOrganizationSubscriptionCommand"/> and saves
    /// <see cref="OrganizationEntity.PamSeats"/>.
    /// </summary>
    /// <param name="organization">The organization whose seats are changing.</param>
    /// <param name="seats">The total number of Privileged Controls seats being requested.</param>
    /// <returns>The change set that applies the new seat count.</returns>
    /// <exception cref="BadRequestException">Thrown when the request violates a seat rule.</exception>
    Task<OrganizationSubscriptionChangeSet> CreateAsync(OrganizationEntity organization, int seats);
}
