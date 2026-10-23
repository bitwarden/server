using Bit.Core.AdminConsole.Entities;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.PrivilegedControlsSeats;

public enum PrivilegedControlsSeatReservation
{
    /// <summary>
    /// Seats are available, or the organization is not subject to a Privileged Controls seat limit (it has no
    /// Privileged Controls seats at all).
    /// </summary>
    Reserved,
    /// <summary>
    /// A cloud organization cannot add the seats because it would pass its autoscale limit.
    /// </summary>
    LimitReached,
    /// <summary>
    /// A self-hosted organization does not have enough seats on its license, and seats cannot be autoscaled.
    /// </summary>
    SelfHostedShortfall
}

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
    Task<PrivilegedControlsSeatReservation> RunAsync(Organization organization, int membersTakingSeats);
}

public static class PrivilegedControlsSeatOccupancy
{
    /// <summary>
    /// Whether a member with this status occupies a Privileged Controls seat once granted access.
    /// </summary>
    public static bool OccupiesSeat(OrganizationUserStatusType status) =>
        status is OrganizationUserStatusType.Invited
            or OrganizationUserStatusType.Accepted
            or OrganizationUserStatusType.Confirmed;
}
