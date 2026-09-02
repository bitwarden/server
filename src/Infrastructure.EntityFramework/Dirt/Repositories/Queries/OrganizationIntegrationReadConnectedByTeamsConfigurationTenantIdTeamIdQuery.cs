using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Enums;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories.Queries;

namespace Bit.Infrastructure.EntityFramework.Dirt.Repositories.Queries;

public class OrganizationIntegrationReadConnectedByTeamsConfigurationTenantIdTeamIdQuery : IQuery<OrganizationIntegration>
{
    private readonly string _tenantId;
    private readonly string _teamId;

    public OrganizationIntegrationReadConnectedByTeamsConfigurationTenantIdTeamIdQuery(string tenantId, string teamId)
    {
        _tenantId = tenantId;
        _teamId = teamId;
    }

    public IQueryable<OrganizationIntegration> Run(DatabaseContext dbContext)
    {
        // Matches the JSON path filters in the MSSQL procedure of the same name. A set ChannelId / ServiceUrl
        // serializes with an opening quote, whereas an unset one serializes as null, which is what separates a
        // connected integration from one that is still awaiting (or has lost) its app install.
        var query =
            from oi in dbContext.OrganizationIntegrations
            where oi.Type == IntegrationType.Teams &&
                  oi.Configuration != null &&
                  oi.Configuration.Contains($"\"TenantId\":\"{_tenantId}\"") &&
                  oi.Configuration.Contains($"\"id\":\"{_teamId}\"") &&
                  oi.Configuration.Contains("\"ChannelId\":\"") &&
                  oi.Configuration.Contains("\"ServiceUrl\":\"") &&
                  !oi.Configuration.Contains("\"DisconnectedDate\":\"")
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
