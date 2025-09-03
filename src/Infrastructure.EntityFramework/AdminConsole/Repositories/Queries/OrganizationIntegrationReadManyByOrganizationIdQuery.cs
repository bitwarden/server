using Bit.Core.AdminConsole.Entities;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories.Queries;

namespace Bit.Infrastructure.EntityFramework.AdminConsole.Repositories.Queries;

public class OrganizationIntegrationReadManyByOrganizationIdQuery : IQuery<OrganizationIntegration>
{
    private readonly Guid _organizationId;

    public OrganizationIntegrationReadManyByOrganizationIdQuery(Guid organizationId)
    {
        _organizationId = organizationId;
    }

    public IQueryable<OrganizationIntegration> Run(DatabaseContext dbContext)
    {
        var query = from oi in dbContext.OrganizationIntegrations
                    where oi.OrganizationId == _organizationId
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
