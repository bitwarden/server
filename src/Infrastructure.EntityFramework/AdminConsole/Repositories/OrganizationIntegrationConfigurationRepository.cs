using AutoMapper;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models;
using Bit.Infrastructure.EntityFramework.AdminConsole.Repositories.Queries;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories.Queries;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.AdminConsole.Repositories;

public class OrganizationIntegrationConfigurationRepository : Repository<Core.AdminConsole.Entities.OrganizationIntegrationConfiguration, OrganizationIntegrationConfiguration, Guid>, IOrganizationIntegrationConfigurationRepository
{
    public OrganizationIntegrationConfigurationRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, context => context.OrganizationIntegrationConfigurations)
    { }

    public async Task<List<OrganizationIntegrationConfigurationDetails>> GetConfigurationDetailsAsync(
        Guid organizationId,
        IntegrationType integrationType,
        EventType eventType)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = new OrganizationIntegrationConfigurationDetailsReadManyByEventTypeOrganizationIdIntegrationTypeQuery(
                organizationId,
                eventType,
                integrationType
                );
            return await query.Run(dbContext).ToListAsync();
        }
    }

    public async Task<List<OrganizationIntegrationConfigurationDetails>> GetWildcardConfigurationDetailsAsync(Guid organizationId, IntegrationType integrationType)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = new OrganizationIntegrationConfigurationDetailsReadManyWildcardByOrganizationIdIntegrationTypeQuery(
                organizationId,
                integrationType
            );
            return await query.Run(dbContext).ToListAsync();
        }
    }

    public async Task<List<OrganizationIntegrationConfigurationDetails>> GetAllConfigurationDetailsAsync()
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = new OrganizationIntegrationConfigurationDetailsReadManyQuery();
            return await query.Run(dbContext).ToListAsync();
        }
    }

    public async Task<List<Core.AdminConsole.Entities.OrganizationIntegrationConfiguration>> GetManyByIntegrationAsync(
        Guid organizationIntegrationId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = new OrganizationIntegrationConfigurationReadManyByOrganizationIntegrationIdQuery(
                organizationIntegrationId
            );
            return await query.Run(dbContext).ToListAsync();
        }
    }
}
