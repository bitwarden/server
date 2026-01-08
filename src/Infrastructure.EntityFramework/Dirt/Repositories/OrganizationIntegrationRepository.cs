using AutoMapper;
using Bit.Core.Dirt.Repositories;
using Bit.Infrastructure.EntityFramework.Dirt.Repositories.Queries;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrganizationIntegration = Bit.Core.Dirt.Entities.OrganizationIntegration;

namespace Bit.Infrastructure.EntityFramework.Dirt.Repositories;

public class OrganizationIntegrationRepository :
    Repository<OrganizationIntegration, Dirt.Models.OrganizationIntegration, Guid>,
    IOrganizationIntegrationRepository
{
    public OrganizationIntegrationRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.OrganizationIntegrations)
    {
    }

    public async Task<List<OrganizationIntegration>> GetManyByOrganizationAsync(Guid organizationId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = new OrganizationIntegrationReadManyByOrganizationIdQuery(organizationId);
            return await query.Run(dbContext).ToListAsync();
        }
    }

    public async Task<OrganizationIntegration?> GetByTeamsConfigurationTenantIdTeamId(
        string tenantId,
        string teamId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = new OrganizationIntegrationReadByTeamsConfigurationTenantIdTeamIdQuery(tenantId: tenantId, teamId: teamId);
            return await query.Run(dbContext).SingleOrDefaultAsync();
        }
    }
}
