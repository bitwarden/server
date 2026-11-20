#nullable enable

using AutoMapper;
using Bit.Core.Auth.Repositories;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Auth.Models;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.Auth.Repositories;

/// <inheritdoc cref="IAgentConnectionRepository"/>
public class AgentConnectionRepository : Repository<Core.Auth.Entities.AgentConnection, AgentConnection, Guid>, IAgentConnectionRepository
{
    public AgentConnectionRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.AgentConnections)
    { }

    public override async Task<Core.Auth.Entities.AgentConnection> CreateAsync(Core.Auth.Entities.AgentConnection agentConnection)
    {
        agentConnection = await base.CreateAsync(agentConnection);

        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            await dbContext.UserBumpAccountRevisionDateAsync(agentConnection.UserId);
            await dbContext.SaveChangesAsync();
        }

        return agentConnection;
    }

    public override async Task ReplaceAsync(Core.Auth.Entities.AgentConnection agentConnection)
    {
        await base.ReplaceAsync(agentConnection);

        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            await dbContext.UserBumpAccountRevisionDateAsync(agentConnection.UserId);
            await dbContext.SaveChangesAsync();
        }
    }

    public override async Task DeleteAsync(Core.Auth.Entities.AgentConnection agentConnection)
    {
        await base.DeleteAsync(agentConnection);

        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            await dbContext.UserBumpAccountRevisionDateAsync(agentConnection.UserId);
            await dbContext.SaveChangesAsync();
        }
    }

    /// <inheritdoc />
    public async Task<ICollection<Core.Auth.Entities.AgentConnection>> GetManyByUserIdAsync(Guid userId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var results = await dbContext.AgentConnections
                .Where(c => c.UserId == userId)
                .ToListAsync();
            return Mapper.Map<List<Core.Auth.Entities.AgentConnection>>(results);
        }
    }

    /// <inheritdoc />
    public DatabaseTransactionAction UpdateForKeyRotation(Guid userId,
        IEnumerable<Core.Auth.Entities.AgentConnection> connections)
    {
        return async (connection, transaction) =>
        {
            var newConnections = connections.ToDictionary(c => c.Id);
            using var scope = ServiceScopeFactory.CreateScope();
            var dbContext = GetTransactionalDatabaseContext(scope, connection, transaction);
            var userConnections = await GetDbSet(dbContext)
                .Where(c => c.UserId == userId)
                .ToListAsync();
            var validConnections = userConnections
                .Where(c => newConnections.ContainsKey(c.Id));
            foreach (var agentConnection in validConnections)
            {
                agentConnection.Key = newConnections[agentConnection.Id].Key;
            }

            await dbContext.SaveChangesAsync();
        };
    }
}
