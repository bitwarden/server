using Bit.Core.AdminConsole.Entities;
using Bit.Core.Repositories;

namespace Bit.Core.Billing.Organizations.Queries;

public interface ICountNewPamSeatsRequiredQuery
{
    /// <summary>
    /// Returns how many Privileged Controls seats the organization lacks to grant access to <paramref name="usersToAdd"/>
    /// more members. Returns 0 when <see cref="Organization.PamSeats"/> is null: licenses issued before the seat count
    /// existed carry no value and must keep working, so the grant is not limited. Does not check
    /// <see cref="Organization.UsePam"/>; callers reject members of organizations without Privileged Controls before
    /// asking for a count, so this stays a pure count.
    /// </summary>
    Task<int> CountNewPamSeatsRequiredAsync(Organization organization, int usersToAdd);
}

public class CountNewPamSeatsRequiredQuery(
    IOrganizationUserRepository organizationUserRepository) : ICountNewPamSeatsRequiredQuery
{
    public async Task<int> CountNewPamSeatsRequiredAsync(Organization organization, int usersToAdd)
    {
        if (usersToAdd == 0)
        {
            return 0;
        }

        if (!organization.PamSeats.HasValue)
        {
            return 0;
        }

        var occupiedPamSeats =
            await organizationUserRepository.GetOccupiedPamSeatCountByOrganizationIdAsync(organization.Id);

        var availablePamSeats = organization.PamSeats.Value - occupiedPamSeats;

        if (availablePamSeats >= usersToAdd)
        {
            return 0;
        }

        return usersToAdd - availablePamSeats;
    }
}
