using AutoMapper;
using Bit.Core.Dirt.Enums;
using Bit.Core.Dirt.Models.Data.EventIntegrations;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Enums;
using Bit.Infrastructure.EntityFramework.Dirt.Repositories.Queries;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrganizationIntegrationConfiguration = Bit.Core.Dirt.Entities.OrganizationIntegrationConfiguration;

namespace Bit.Infrastructure.EntityFramework.Dirt.Repositories;

public class OrganizationIntegrationConfigurationRepository : Repository<OrganizationIntegrationConfiguration, Dirt.Models.OrganizationIntegrationConfiguration, Guid>, IOrganizationIntegrationConfigurationRepository
{
    public OrganizationIntegrationConfigurationRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, context => context.OrganizationIntegrationConfigurations)
    { }

    public async Task<List<OrganizationIntegrationConfigurationDetails>>
        GetManyByEventTypeOrganizationIdIntegrationType(EventType eventType, Guid organizationId,
            IntegrationType integrationType)
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

    public async Task<List<OrganizationIntegrationConfigurationDetails>> GetAllConfigurationDetailsAsync()
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = new OrganizationIntegrationConfigurationDetailsReadManyQuery();
            return await query.Run(dbContext).ToListAsync();
        }
    }

    public async Task<List<OrganizationIntegrationConfiguration>> GetManyByIntegrationAsync(
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

    public async Task<bool> DisableAsync(Guid id, DateTime disabledDate, IntegrationFailureCategory disabledReason)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);

            // Filtering on the enabled state keeps concurrent trips across instances to a single transition
            var rowsAffected = await dbContext.OrganizationIntegrationConfigurations
                .Where(configuration => configuration.Id == id && configuration.DisabledDate == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(configuration => configuration.DisabledDate, disabledDate)
                    .SetProperty(configuration => configuration.DisabledReason, disabledReason)
                    .SetProperty(configuration => configuration.RevisionDate, disabledDate));

            return rowsAffected > 0;
        }
    }

}
