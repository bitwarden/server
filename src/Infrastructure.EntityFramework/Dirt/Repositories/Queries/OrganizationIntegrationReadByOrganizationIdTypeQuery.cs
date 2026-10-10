using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Enums;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories.Queries;

namespace Bit.Infrastructure.EntityFramework.Dirt.Repositories.Queries;

public class OrganizationIntegrationReadByOrganizationIdTypeQuery : IQuery<OrganizationIntegration>
{
    private readonly Guid _organizationId;
    private readonly IntegrationType _type;

    public OrganizationIntegrationReadByOrganizationIdTypeQuery(Guid organizationId, IntegrationType type)
    {
        _organizationId = organizationId;
        _type = type;
    }

    public IQueryable<OrganizationIntegration> Run(DatabaseContext dbContext)
    {
        var query = from oi in dbContext.OrganizationIntegrations
                    where oi.OrganizationId == _organizationId && oi.Type == _type
                    select new OrganizationIntegration()
                    {
                        Id = oi.Id,
                        OrganizationId = oi.OrganizationId,
                        Type = oi.Type,
                        Configuration = oi.Configuration,
                    };
        return query;
    }
}
