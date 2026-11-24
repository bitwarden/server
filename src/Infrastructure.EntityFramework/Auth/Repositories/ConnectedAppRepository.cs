#nullable enable

using AutoMapper;
using Bit.Core.Auth.Repositories;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Auth.Models;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.Auth.Repositories;

/// <inheritdoc cref="IConnectedAppRepository"/>
public class ConnectedAppRepository : Repository<Core.Auth.Entities.ConnectedApp, ConnectedApp, Guid>, IConnectedAppRepository
{
    public ConnectedAppRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.ConnectedApps)
    { }

    public override async Task<Core.Auth.Entities.ConnectedApp> CreateAsync(Core.Auth.Entities.ConnectedApp connectedApp)
    {
        connectedApp = await base.CreateAsync(connectedApp);

        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            await dbContext.UserBumpAccountRevisionDateAsync(connectedApp.UserId);
            await dbContext.SaveChangesAsync();
        }

        return connectedApp;
    }

    public override async Task ReplaceAsync(Core.Auth.Entities.ConnectedApp connectedApp)
    {
        await base.ReplaceAsync(connectedApp);

        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            await dbContext.UserBumpAccountRevisionDateAsync(connectedApp.UserId);
            await dbContext.SaveChangesAsync();
        }
    }

    public override async Task DeleteAsync(Core.Auth.Entities.ConnectedApp connectedApp)
    {
        await base.DeleteAsync(connectedApp);

        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            await dbContext.UserBumpAccountRevisionDateAsync(connectedApp.UserId);
            await dbContext.SaveChangesAsync();
        }
    }

    /// <inheritdoc />
    public async Task<ICollection<Core.Auth.Entities.ConnectedApp>> GetManyByUserIdAsync(Guid userId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var results = await dbContext.ConnectedApps
                .Where(c => c.UserId == userId)
                .ToListAsync();
            return Mapper.Map<List<Core.Auth.Entities.ConnectedApp>>(results);
        }
    }

    /// <inheritdoc />
    public DatabaseTransactionAction UpdateForKeyRotation(Guid userId,
        IEnumerable<Core.Auth.Entities.ConnectedApp> connections)
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
            foreach (var connectedApp in validConnections)
            {
                connectedApp.Key = newConnections[connectedApp.Id].Key;
            }

            await dbContext.SaveChangesAsync();
        };
    }
}
