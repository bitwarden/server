using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.OrganizationFeatures.OrganizationSmSubscription.Interface;
using Bit.Core.Repositories;

namespace Bit.Core.OrganizationFeatures.OrganizationSmSubscription;

public class GetOrganizationQuery : IGetOrganizationQuery
{
    private readonly IOrganizationRepository _organizationRepository;

    public GetOrganizationQuery(IOrganizationRepository organizationRepository)
    {
        _organizationRepository = organizationRepository;
    }

    public async Task<Organization> GetOrgById(Guid organizationId)
    {
        return await _organizationRepository.GetByIdAsync(organizationId);
    }
}
