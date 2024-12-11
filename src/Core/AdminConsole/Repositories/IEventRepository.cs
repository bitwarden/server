using Bit.Core.Models.Data;
using Bit.Core.Vault.Entities;

#nullable enable

namespace Bit.Core.Repositories;

public interface IEventRepository
{
    Task<PagedResult<IEvent>> GetManyByUserAsync(
        Guid userId,
        DateTime startDate,
        DateTime endDate,
        PageOptions pageOptions
    );
    Task<PagedResult<IEvent>> GetManyByOrganizationAsync(
        Guid organizationId,
        DateTime startDate,
        DateTime endDate,
        PageOptions pageOptions
    );
    Task<PagedResult<IEvent>> GetManyByOrganizationActingUserAsync(
        Guid organizationId,
        Guid actingUserId,
        DateTime startDate,
        DateTime endDate,
        PageOptions pageOptions
    );
    Task<PagedResult<IEvent>> GetManyByProviderAsync(
        Guid providerId,
        DateTime startDate,
        DateTime endDate,
        PageOptions pageOptions
    );
    Task<PagedResult<IEvent>> GetManyByProviderActingUserAsync(
        Guid providerId,
        Guid actingUserId,
        DateTime startDate,
        DateTime endDate,
        PageOptions pageOptions
    );
    Task<PagedResult<IEvent>> GetManyByCipherAsync(
        Cipher cipher,
        DateTime startDate,
        DateTime endDate,
        PageOptions pageOptions
    );
    Task CreateAsync(IEvent e);
    Task CreateManyAsync(IEnumerable<IEvent> e);
    Task<PagedResult<IEvent>> GetManyByOrganizationServiceAccountAsync(
        Guid organizationId,
        Guid serviceAccountId,
        DateTime startDate,
        DateTime endDate,
        PageOptions pageOptions
    );
}
