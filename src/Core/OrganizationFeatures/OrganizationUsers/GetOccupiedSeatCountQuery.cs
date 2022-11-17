using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Repositories;

namespace Bit.Core.OrganizationFeatures.OrganizationUsers;

public class GetOccupiedSeatCountQuery : IGetOccupiedSeatCountQuery
{
    private readonly IOrganizationUserRepository _organizationUserRepository;

    GetOccupiedSeatCountQuery(IOrganizationUserRepository organizationUserRepository)
    {
        _organizationUserRepository = organizationUserRepository;
    }

    public async Task<int> GetOccupiedSeatCountAsync(Organization organization)
    {
        return await _organizationUserRepository.GetCountByMinimumStatusOrganizationAsync(organization.Id,
            OrganizationUserStatusType.Invited);
    }
}
