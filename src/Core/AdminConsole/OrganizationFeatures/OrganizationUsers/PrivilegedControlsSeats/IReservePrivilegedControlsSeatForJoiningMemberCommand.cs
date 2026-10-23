using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.PrivilegedControlsSeats;

/// <summary>
/// Makes sure a member who is joining an organization with Privileged Controls access has a seat for it.
/// </summary>
public interface IReservePrivilegedControlsSeatForJoiningMemberCommand
{
    /// <summary>
    /// Returns true when the member keeps Privileged Controls access. When no seat is available, sets
    /// <see cref="OrganizationUser.AccessPam"/> to false in memory, emails the organization's Owners, and returns
    /// false. The caller saves the member in its existing save.
    /// </summary>
    Task<bool> RunAsync(Organization organization, OrganizationUser organizationUser, string memberEmail);
}
