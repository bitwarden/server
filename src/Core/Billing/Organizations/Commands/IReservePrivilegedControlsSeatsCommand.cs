using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Organizations.Models;

namespace Bit.Core.Billing.Organizations.Commands;

/// <summary>
/// Makes sure an organization has Privileged Controls seats for members who are about to be given access.
/// </summary>
public interface IReservePrivilegedControlsSeatsCommand
{
    /// <summary>
    /// Reserves Privileged Controls seats for members who are about to be granted access. On cloud, seats are
    /// autoscaled up to the organization's limit. The caller grants the access.
    /// </summary>
    /// <param name="organization">The organization the members belong to.</param>
    /// <param name="seatsRequested">
    /// The number of members about to start using a Privileged Controls seat. Not the new total and not the
    /// shortfall: the command buys only what free seats don't cover.
    /// </param>
    /// <remarks>
    /// Succeeds without checking seats when no seats are requested, the organization doesn't use Privileged
    /// Controls, or it has no Privileged Controls seat count. On cloud it also succeeds without checking seats when
    /// <see cref="FeatureFlagKeys.PM39883_Pam_Billing_Support"/> is off. Never throws: every failure is returned as
    /// an error.
    /// </remarks>
    Task<PrivilegedControlsSeatReservationResult> RunAsync(Organization organization, int seatsRequested);
}
