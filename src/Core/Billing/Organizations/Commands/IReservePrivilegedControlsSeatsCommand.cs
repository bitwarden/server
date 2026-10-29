using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Organizations.Models;

namespace Bit.Core.Billing.Organizations.Commands;

/// <summary>
/// Makes sure an organization has Privileged Controls seats for members who are about to be given access.
/// </summary>
public interface IReservePrivilegedControlsSeatsCommand
{
    /// <summary>
    /// Reserves seats for <paramref name="membersTakingSeats"/> members who are about to be granted Privileged
    /// Controls access and do not already occupy a seat. On cloud, seats are autoscaled up to the organization's limit.
    /// The caller grants the access.
    /// </summary>
    /// <remarks>
    /// Succeeds without checking seats when no members are taking seats, the organization doesn't use Privileged
    /// Controls, or it has no Privileged Controls seat count. Never throws: every failure is returned as an error.
    /// </remarks>
    Task<PrivilegedControlsSeatReservationResult> RunAsync(Organization organization, int membersTakingSeats);
}
