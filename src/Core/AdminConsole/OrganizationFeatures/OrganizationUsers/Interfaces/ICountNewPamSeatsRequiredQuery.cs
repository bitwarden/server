using Bit.Core.AdminConsole.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;

public interface ICountNewPamSeatsRequiredQuery
{
    /// <summary>
    /// Returns how many Privileged Controls seats the organization lacks to grant access to <paramref name="usersToAdd"/>
    /// more members. Returns 0 when <see cref="Organization.PamSeats"/> is null: licenses issued before the seat count
    /// existed carry no value and must keep working, so the grant is not limited. Does not check
    /// <see cref="Organization.UsePam"/>; callers reject members of organizations without Privileged Controls before
    /// asking for a count, so this stays a pure count.
    /// </summary>
    public Task<int> CountNewPamSeatsRequiredAsync(Organization organization, int usersToAdd);
}
