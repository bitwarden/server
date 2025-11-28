using AutoMapper;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

#nullable enable

namespace Bit.Infrastructure.EntityFramework.Repositories;

public class OrganizationConnectionRepository : Repository<OrganizationConnection, Models.OrganizationConnection, Guid>, IOrganizationConnectionRepository
{
    public OrganizationConnectionRepository(IServiceScopeFactory serviceScopeFactory,
        IMapper mapper)
        : base(serviceScopeFactory, mapper, context => context.OrganizationConnections)
    {
    }

    public async Task<OrganizationConnection?> GetByIdOrganizationIdAsync(Guid id, Guid organizationId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var connection = await dbContext.OrganizationConnections
                .FirstOrDefaultAsync(oc => oc.Id == id && oc.OrganizationId == organizationId);
            return Mapper.Map<OrganizationConnection>(connection);
        }
    }

    public async Task<ICollection<OrganizationConnection>> GetByOrganizationIdTypeAsync(Guid organizationId, OrganizationConnectionType type)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var connections = await dbContext.OrganizationConnections
                .Where(oc => oc.OrganizationId == organizationId && oc.Type == type)
                .ToListAsync();
            return Mapper.Map<List<OrganizationConnection>>(connections);
        }
    }

    public async Task<ICollection<OrganizationConnection>> GetEnabledByOrganizationIdTypeAsync(Guid organizationId, OrganizationConnectionType type)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var connections = await dbContext.OrganizationConnections
                .Where(oc => oc.OrganizationId == organizationId && oc.Type == type && oc.Enabled)
                .ToListAsync();
            return Mapper.Map<List<OrganizationConnection>>(connections);
        }
    }
}
