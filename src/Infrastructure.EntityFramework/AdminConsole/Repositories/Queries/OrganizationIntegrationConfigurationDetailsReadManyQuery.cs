#nullable enable

using Bit.Core.Models.Data.Organizations;

namespace Bit.Infrastructure.EntityFramework.Repositories.Queries;

public class OrganizationIntegrationConfigurationDetailsReadManyQuery : IQuery<OrganizationIntegrationConfigurationDetails>
{
    public IQueryable<OrganizationIntegrationConfigurationDetails> Run(DatabaseContext dbContext)
    {
        var query = from oic in dbContext.OrganizationIntegrationConfigurations
                    join oi in dbContext.OrganizationIntegrations on oic.OrganizationIntegrationId equals oi.Id into oioic
                    from oi in dbContext.OrganizationIntegrations
                    select new OrganizationIntegrationConfigurationDetails()
                    {
                        Id = oic.Id,
                        OrganizationId = oi.OrganizationId,
                        OrganizationIntegrationId = oic.OrganizationIntegrationId,
                        IntegrationType = oi.Type,
                        EventType = oic.EventType,
                        Configuration = oic.Configuration,
                        Filters = oic.Filters,
                        IntegrationConfiguration = oi.Configuration,
                        Template = oic.Template
                    };
        return query;
    }
}
