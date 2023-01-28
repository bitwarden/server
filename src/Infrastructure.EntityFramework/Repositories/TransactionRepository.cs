using AutoMapper;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.Repositories;

public class TransactionRepository : Repository<Core.Entities.Transaction, Transaction, Guid>, ITransactionRepository
{
    public TransactionRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.Transactions)
    { }

    public async Task<Core.Entities.Transaction> GetByGatewayIdAsync(GatewayType gatewayType, string gatewayId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var results = await dbContext.Transactions
                .FirstOrDefaultAsync(t => (t.GatewayId == gatewayId && t.Gateway == gatewayType));
            return Mapper.Map<Core.Entities.Transaction>(results);
        }
    }

    public async Task<ICollection<Core.Entities.Transaction>> GetManyByOrganizationIdAsync(Guid organizationId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var results = await dbContext.Transactions
                .Where(t => (t.OrganizationId == organizationId && !t.UserId.HasValue))
                .ToListAsync();
            return Mapper.Map<List<Core.Entities.Transaction>>(results);
        }
    }

    public async Task<ICollection<Core.Entities.Transaction>> GetManyByUserIdAsync(Guid userId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var results = await dbContext.Transactions
                .Where(t => (t.UserId == userId))
                .ToListAsync();
            return Mapper.Map<List<Core.Entities.Transaction>>(results);
        }
    }
}
