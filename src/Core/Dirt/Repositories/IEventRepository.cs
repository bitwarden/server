using Bit.Core.Models.Data;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.Vault.Entities;

#nullable enable

namespace Bit.Core.Repositories;

public interface IEventRepository
{
    Task<PagedResult<IEvent>> GetManyByUserAsync(Guid userId, DateTime startDate, DateTime endDate,
        PageOptions pageOptions);
    Task<PagedResult<IEvent>> GetManyByOrganizationAsync(Guid organizationId, DateTime startDate, DateTime endDate,
        PageOptions pageOptions);

    Task<PagedResult<IEvent>> GetManyBySecretAsync(Secret secret, DateTime startDate, DateTime endDate,
        PageOptions pageOptions);

    Task<PagedResult<IEvent>> GetManyByProjectAsync(Project project, DateTime startDate, DateTime endDate,
        PageOptions pageOptions);

    Task<PagedResult<IEvent>> GetManyByOrganizationActingUserAsync(Guid organizationId, Guid actingUserId,
        DateTime startDate, DateTime endDate, PageOptions pageOptions);
    Task<PagedResult<IEvent>> GetManyByProviderAsync(Guid providerId, DateTime startDate, DateTime endDate,
        PageOptions pageOptions);
    Task<PagedResult<IEvent>> GetManyByProviderActingUserAsync(Guid providerId, Guid actingUserId,
        DateTime startDate, DateTime endDate, PageOptions pageOptions);
    Task<PagedResult<IEvent>> GetManyByCipherAsync(Cipher cipher, DateTime startDate, DateTime endDate,
        PageOptions pageOptions);

    Task CreateAsync(IEvent e);
    Task CreateManyAsync(IEnumerable<IEvent> e);
    Task<PagedResult<IEvent>> GetManyByOrganizationServiceAccountAsync(Guid organizationId, Guid serviceAccountId,
        DateTime startDate, DateTime endDate, PageOptions pageOptions);

    /// <summary>
    /// Deletes all events for the given organization and returns the number deleted.
    /// Used to purge orphaned event logs when an organization is deleted (GDPR).
    /// </summary>
    Task<int> DeleteManyByOrganizationIdAsync(Guid organizationId);
}
