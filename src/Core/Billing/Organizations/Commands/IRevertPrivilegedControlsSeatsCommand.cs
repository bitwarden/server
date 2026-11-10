using Bit.Core.Exceptions;
using OrganizationEntity = Bit.Core.AdminConsole.Entities.Organization;

namespace Bit.Core.Billing.Organizations.Commands;

/// <summary>
/// Gives back Privileged Controls seats bought earlier in a request, for when a later step fails.
/// </summary>
public interface IRevertPrivilegedControlsSeatsCommand
{
    /// <summary>
    /// Puts the organization's Privileged Controls seat count back to <paramref name="seats"/>. Nothing changes
    /// when the organization already has that many seats. On a trial the seats are reset on the organization only,
    /// with no change to Stripe. The autoscale limit is not changed.
    /// </summary>
    /// <param name="organization">The organization that already has Privileged Controls.</param>
    /// <param name="seats">The seat count captured before the autoscale.</param>
    /// <exception cref="BadRequestException">
    /// Thrown on a self-hosted instance, or when the seat change or subscription update is rejected.
    /// </exception>
    /// <exception cref="ConflictException">Thrown when the organization's subscription is in a state that prevents the change.</exception>
    Task RunAsync(OrganizationEntity organization, int seats);
}
