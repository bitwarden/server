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

        public Task<Transaction> GetByGatewayIdAsync(GatewayType gatewayType, string gatewayId)
        {
            throw new NotImplementedException();
        }

        public Task<ICollection<Transaction>> GetManyByOrganizationIdAsync(Guid organizationId)
        {
            throw new NotImplementedException();
        }

        public Task<ICollection<Transaction>> GetManyByUserIdAsync(Guid userId)
        {
            throw new NotImplementedException();
        }
    }
}
