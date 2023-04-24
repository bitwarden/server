using Bit.Core.Entities.Provider;

namespace Bit.Infrastructure.EntityFramework.Repositories.Queries;

public class ProviderOrganizationCountByOrganizationIdsQuery : IQuery<ProviderOrganization>
{
    private readonly IEnumerable<Guid> _organizationIds;

    public ProviderOrganizationCountByOrganizationIdsQuery(IEnumerable<Guid> organizationIds)
    {
        _organizationIds = organizationIds;
    }

    public IQueryable<ProviderOrganization> Run(DatabaseContext dbContext)
    {
        var query = from po in dbContext.ProviderOrganizations
                    where _organizationIds.Contains(po.OrganizationId)
                    select po;
        return query;
    }
}
