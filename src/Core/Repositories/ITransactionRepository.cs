using Bit.Core.Entities;
using Bit.Core.Enums;

#nullable enable

namespace Bit.Core.Repositories;

public interface ITransactionRepository : IRepository<Transaction, Guid>
{
    Task<ICollection<Transaction>> GetManyByUserIdAsync(
        Guid userId,
        int? limit = null,
        DateTime? startAfter = null
    );
    Task<ICollection<Transaction>> GetManyByOrganizationIdAsync(
        Guid organizationId,
        int? limit = null,
        DateTime? startAfter = null
    );
    Task<ICollection<Transaction>> GetManyByProviderIdAsync(
        Guid providerId,
        int? limit = null,
        DateTime? startAfter = null
    );
    Task<Transaction?> GetByGatewayIdAsync(GatewayType gatewayType, string gatewayId);
}
