using Bit.Core.Entities;
using Bit.Core.OrganizationFeatures.OrganizationSubscription.Interface;
using Bit.Core.Repositories;

namespace Bit.Core.OrganizationFeatures.OrganizationSubscription;

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
