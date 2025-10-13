using AutoMapper;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models;
using Bit.Infrastructure.EntityFramework.AdminConsole.Repositories.Queries;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.AdminConsole.Repositories;

public class OrganizationIntegrationRepository :
    Repository<Core.AdminConsole.Entities.OrganizationIntegration, OrganizationIntegration, Guid>,
    IOrganizationIntegrationRepository
{
    public OrganizationIntegrationRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.OrganizationIntegrations)
    {
    }

    public async Task<List<Core.AdminConsole.Entities.OrganizationIntegration>> GetManyByOrganizationAsync(Guid organizationId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = new OrganizationIntegrationReadManyByOrganizationIdQuery(organizationId);
            return await query.Run(dbContext).ToListAsync();
        }
    }

    public async Task<Core.AdminConsole.Entities.OrganizationIntegration?> GetByTeamsConfigurationTenantIdTeamId(
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
