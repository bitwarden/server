using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Bit.Core.Enums;
using Bit.Core.Models.Table;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DataModel = Bit.Core.Models.Data;
using EfModel = Bit.Core.Models.EntityFramework;
using TableModel = Bit.Core.Models.Table;

namespace Bit.Core.Repositories.EntityFramework
{
    public class TransactionRepository : Repository<TableModel.Transaction, EfModel.Transaction, Guid>, ITransactionRepository
    {
        public TransactionRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
            : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.Transactions)
        { }

        public async Task<Transaction> GetByGatewayIdAsync(GatewayType gatewayType, string gatewayId)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var results = await dbContext.Transactions
                    .FirstOrDefaultAsync(t => (t.GatewayId == gatewayId && t.Gateway == gatewayType));
                return results;
            }
        }

        public async Task<ICollection<Transaction>> GetManyByOrganizationIdAsync(Guid organizationId)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var results = await dbContext.Transactions
                    .Where(t => (t.OrganizationId == organizationId && !t.UserId.HasValue))
                    .ToListAsync();
                return (ICollection<Transaction>)results;
            }
        }

        public async Task<ICollection<Transaction>> GetManyByUserIdAsync(Guid userId)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var results = await dbContext.Transactions
                    .Where(t => (t.UserId == userId))
                    .ToListAsync();
                return (ICollection<Transaction>)results;
            }
        }
    }
}
