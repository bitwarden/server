using System;
using Bit.Core.Models.Table;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Enums;

namespace Bit.Core.Repositories
{
    public interface ITransactionRepository : IRepository<Transaction, Guid>
    {
        Task<ICollection<Transaction>> GetManyByUserIdAsync(Guid userId);
        Task<ICollection<Transaction>> GetManyByOrganizationIdAsync(Guid organizationId);
        Task<Transaction> GetByGatewayIdAsync(GatewayType gatewayType, string gatewayId);
    }
}
