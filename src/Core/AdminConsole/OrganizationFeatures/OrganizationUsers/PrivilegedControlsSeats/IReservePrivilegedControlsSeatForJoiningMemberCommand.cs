using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.PrivilegedControlsSeats;

public enum JoiningMemberPrivilegedControlsAccess
{
    /// <summary>
    /// The member keeps Privileged Controls access.
    /// </summary>
    Kept,
    /// <summary>
    /// The organization has no Privileged Controls seat left, so the member joins without access.
    /// </summary>
    RemovedAtSeatLimit,
    /// <summary>
    /// Reserving the seat failed, so the member joins without access.
    /// </summary>
    RemovedAfterError
}

/// <summary>
/// Makes sure a member who is joining an organization with Privileged Controls access has a seat for it.
/// </summary>
public interface IReservePrivilegedControlsSeatForJoiningMemberCommand
{
    /// <summary>
    /// Reserves a Privileged Controls seat for the member. When no seat can be reserved, sets
    /// <see cref="OrganizationUser.AccessPam"/> to false in memory so the member can still join. The caller saves
    /// the member in its existing save.
    /// </summary>
    Task<JoiningMemberPrivilegedControlsAccess> RunAsync(Organization organization, OrganizationUser organizationUser);

    /// <summary>
    /// Emails the organization's Owners that the member joined without Privileged Controls access. Call after the
    /// member is saved, when <see cref="RunAsync"/> returned <see cref="JoiningMemberPrivilegedControlsAccess.RemovedAtSeatLimit"/>.
    /// Does nothing on self-hosted.
    /// </summary>
    Task NotifyOwnersAsync(Organization organization, string memberEmail);
}
