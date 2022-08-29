using Bit.Core.Entities;
using Bit.Core.Enums;

namespace Bit.Core.Repositories;

public interface ITransactionRepository : IRepository<Transaction, Guid>
{
    Task<ICollection<Transaction>> GetManyByUserIdAsync(Guid userId);
    Task<ICollection<Transaction>> GetManyByOrganizationIdAsync(Guid organizationId);
    Task<Transaction> GetByGatewayIdAsync(GatewayType gatewayType, string gatewayId);
}
