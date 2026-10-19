using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;

public class CountNewPamSeatsRequiredQuery : ICountNewPamSeatsRequiredQuery
{
    private readonly IOrganizationUserRepository _organizationUserRepository;

    public CountNewPamSeatsRequiredQuery(IOrganizationUserRepository organizationUserRepository)
    {
        _organizationUserRepository = organizationUserRepository;
    }

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
            await _organizationUserRepository.GetOccupiedPamSeatCountByOrganizationIdAsync(organization.Id);

        var availablePamSeats = organization.PamSeats.Value - occupiedPamSeats;

        if (availablePamSeats >= usersToAdd)
        {
            return 0;
        }

        return usersToAdd - availablePamSeats;
    }
}
