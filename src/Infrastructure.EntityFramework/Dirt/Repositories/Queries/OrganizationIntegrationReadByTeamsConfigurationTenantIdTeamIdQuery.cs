using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Enums;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories.Queries;

namespace Bit.Infrastructure.EntityFramework.Dirt.Repositories.Queries;

public class OrganizationIntegrationReadByTeamsConfigurationTenantIdTeamIdQuery : IQuery<OrganizationIntegration>
{
    private readonly string _tenantId;
    private readonly string _teamId;

    public OrganizationIntegrationReadByTeamsConfigurationTenantIdTeamIdQuery(string tenantId, string teamId)
    {
        _tenantId = tenantId;
        _teamId = teamId;
    }

    public IQueryable<OrganizationIntegration> Run(DatabaseContext dbContext)
    {
        var query =
            from oi in dbContext.OrganizationIntegrations
            where oi.Type == IntegrationType.Teams &&
                  oi.Configuration != null &&
                  oi.Configuration.Contains($"\"TenantId\":\"{_tenantId}\"") &&
                  oi.Configuration.Contains($"\"id\":\"{_teamId}\"")
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
