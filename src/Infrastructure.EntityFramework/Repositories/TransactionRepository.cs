﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TableModel = Bit.Core.Models.Table;

namespace Bit.Infrastructure.EntityFramework.Repositories
{
    public class TransactionRepository : Repository<TableModel.Transaction, Transaction, Guid>, ITransactionRepository
    {
        public TransactionRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
            : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.Transactions)
        { }

        public async Task<TableModel.Transaction> GetByGatewayIdAsync(GatewayType gatewayType, string gatewayId)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var results = await dbContext.Transactions
                    .FirstOrDefaultAsync(t => (t.GatewayId == gatewayId && t.Gateway == gatewayType));
                return Mapper.Map<TableModel.Transaction>(results);
            }
        }

        public async Task<ICollection<TableModel.Transaction>> GetManyByOrganizationIdAsync(Guid organizationId)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var results = await dbContext.Transactions
                    .Where(t => (t.OrganizationId == organizationId && !t.UserId.HasValue))
                    .ToListAsync();
                return Mapper.Map<List<TableModel.Transaction>>(results);
            }
        }

        public async Task<ICollection<TableModel.Transaction>> GetManyByUserIdAsync(Guid userId)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var results = await dbContext.Transactions
                    .Where(t => (t.UserId == userId))
                    .ToListAsync();
                return Mapper.Map<List<TableModel.Transaction>>(results);
            }
        }
    }
}
