#nullable enable

using AutoMapper;
using Bit.Core.KeyManagement.UserKey;
using Bit.Core.Tools.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Infrastructure.EntityFramework.Tools.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.Tools.Repositories;

/// <inheritdoc cref="IReceiveRepository"/>
public class ReceiveRepository : Repository<Core.Tools.Entities.Receive, Receive, Guid>, IReceiveRepository
{
    public ReceiveRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.Receives)
    { }

    public override async Task<Core.Tools.Entities.Receive> CreateAsync(Core.Tools.Entities.Receive receive)
    {
        receive = await base.CreateAsync(receive);
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            await dbContext.UserBumpAccountRevisionDateAsync(receive.UserId);
            await dbContext.SaveChangesAsync();
        }

        return receive;
    }

    /// <inheritdoc />
    public async Task<ICollection<Core.Tools.Entities.Receive>> GetManyByExpirationDateAsync(DateTime expirationDateBefore)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var results = await dbContext.Receives.Where(r => r.ExpirationDate < expirationDateBefore).ToListAsync();
            return Mapper.Map<List<Core.Tools.Entities.Receive>>(results);
        }
    }

    /// <inheritdoc />
    public async Task<ICollection<Core.Tools.Entities.Receive>> GetManyByUserIdAsync(Guid userId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var results = await dbContext.Receives.Where(r => r.UserId == userId).ToListAsync();
            return Mapper.Map<List<Core.Tools.Entities.Receive>>(results);
        }
    }

    /// <inheritdoc />
    public UpdateEncryptedDataForKeyRotation UpdateForKeyRotation(Guid userId,
        IEnumerable<Core.Tools.Entities.Receive> receives)
    {
        return async (_, _) =>
        {
            var newReceives = receives.ToDictionary(r => r.Id);
            using var scope = ServiceScopeFactory.CreateScope();
            var dbContext = GetDatabaseContext(scope);
            var userReceives = await GetDbSet(dbContext)
                .Where(r => r.UserId == userId)
                .ToListAsync();
            var validReceives = userReceives
                .Where(receive => newReceives.ContainsKey(receive.Id));
            foreach (var receive in validReceives)
            {
                receive.UserKeyWrappedSharedContentEncryptionKey = newReceives[receive.Id].UserKeyWrappedSharedContentEncryptionKey;
                receive.UserKeyWrappedPrivateKey = newReceives[receive.Id].UserKeyWrappedPrivateKey;
            }

            await dbContext.SaveChangesAsync();
        };
    }
}
