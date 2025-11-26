using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations;

namespace Bit.Infrastructure.EntityFramework.Repositories.Queries;

public class OrganizationIntegrationConfigurationDetailsReadManyWildcardByOrganizationIdIntegrationTypeQuery : IQuery<OrganizationIntegrationConfigurationDetails>
{
    private readonly Guid _organizationId;
    private readonly IntegrationType _integrationType;

    public OrganizationIntegrationConfigurationDetailsReadManyWildcardByOrganizationIdIntegrationTypeQuery(Guid organizationId, IntegrationType integrationType)
    {
        _organizationId = organizationId;
        _integrationType = integrationType;
    }

    public IQueryable<OrganizationIntegrationConfigurationDetails> Run(DatabaseContext dbContext)
    {
        var query = from oic in dbContext.OrganizationIntegrationConfigurations
                    join oi in dbContext.OrganizationIntegrations on oic.OrganizationIntegrationId equals oi.Id into oioic
                    from oi in dbContext.OrganizationIntegrations
                    where oi.OrganizationId == _organizationId &&
                          oi.Type == _integrationType &&
                          !oic.EventType.HasValue
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
