using AutoMapper;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Models;
using LinqToDB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

#nullable enable

namespace Bit.Infrastructure.EntityFramework.Repositories;

public class TransactionRepository : Repository<Core.Entities.Transaction, Transaction, Guid>, ITransactionRepository
{
    public TransactionRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.Transactions)
    { }

    public async Task<Core.Entities.Transaction?> GetByGatewayIdAsync(GatewayType gatewayType, string gatewayId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var results = await EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(dbContext.Transactions, t => (t.GatewayId == gatewayId && t.Gateway == gatewayType));
        return Mapper.Map<Core.Entities.Transaction>(results);
    }

    public async Task<ICollection<Core.Entities.Transaction>> GetManyByOrganizationIdAsync(
        Guid organizationId,
        int? limit = null,
        DateTime? startAfter = null)
    {
        using var scope = ServiceScopeFactory.CreateScope();

        var dbContext = GetDatabaseContext(scope);
        var query = dbContext.Transactions
            .Where(t => t.OrganizationId == organizationId && !t.UserId.HasValue);

        if (startAfter.HasValue)
        {
            query = query.Where(t => t.CreationDate < startAfter.Value);
        }

        if (limit.HasValue)
        {
            query = query.OrderByDescending(o => o.CreationDate).Take(limit.Value);
        }

        var results = await EntityFrameworkQueryableExtensions.ToListAsync(query);
        return Mapper.Map<List<Core.Entities.Transaction>>(results);
    }

    public async Task<ICollection<Core.Entities.Transaction>> GetManyByUserIdAsync(
        Guid userId,
        int? limit = null,
        DateTime? startAfter = null)
    {
        using var scope = ServiceScopeFactory.CreateScope();

        var dbContext = GetDatabaseContext(scope);
        var query = dbContext.Transactions
            .Where(t => t.UserId == userId);

        if (startAfter.HasValue)
        {
            query = query.Where(t => t.CreationDate < startAfter.Value);
        }

        if (limit.HasValue)
        {
            query = query.OrderByDescending(o => o.CreationDate).Take(limit.Value);
        }

        var results = await EntityFrameworkQueryableExtensions.ToListAsync(query);

        return Mapper.Map<List<Core.Entities.Transaction>>(results);
    }

    public async Task<ICollection<Core.Entities.Transaction>> GetManyByProviderIdAsync(
        Guid providerId,
        int? limit = null,
        DateTime? startAfter = null)
    {
        using var serviceScope = ServiceScopeFactory.CreateScope();
        var databaseContext = GetDatabaseContext(serviceScope);
        var query = databaseContext.Transactions
            .Where(transaction => transaction.ProviderId == providerId);

        if (startAfter.HasValue)
        {
            query = query.Where(transaction => transaction.CreationDate < startAfter.Value);
        }

        if (limit.HasValue)
        {
            query = query.Take(limit.Value);
        }

        var results = await EntityFrameworkQueryableExtensions.ToListAsync(query);
        return Mapper.Map<List<Core.Entities.Transaction>>(results);
    }
}
