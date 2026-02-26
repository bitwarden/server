using Bit.Core.Dirt.Entities;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories.Queries;

namespace Bit.Infrastructure.EntityFramework.Dirt.Repositories.Queries;

public class OrganizationIntegrationConfigurationReadManyByOrganizationIntegrationIdQuery : IQuery<OrganizationIntegrationConfiguration>
{
    private readonly Guid _organizationIntegrationId;

    public OrganizationIntegrationConfigurationReadManyByOrganizationIntegrationIdQuery(Guid organizationIntegrationId)
    {
        _organizationIntegrationId = organizationIntegrationId;
    }

    public IQueryable<OrganizationIntegrationConfiguration> Run(DatabaseContext dbContext)
    {
        var query = from oic in dbContext.OrganizationIntegrationConfigurations
                    where oic.OrganizationIntegrationId == _organizationIntegrationId
                    select new OrganizationIntegrationConfiguration()
                    {
                        Id = oic.Id,
                        OrganizationIntegrationId = oic.OrganizationIntegrationId,
                        Configuration = oic.Configuration,
                        EventType = oic.EventType,
                        Filters = oic.Filters,
                        Template = oic.Template,
                        RevisionDate = oic.RevisionDate
                    };
        return query;
    }

}
