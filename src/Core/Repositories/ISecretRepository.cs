using Bit.Core.Entities;

namespace Bit.Core.Repositories
{
    public interface ISecretRepository
    {
        Task<IEnumerable<Secret>> GetManyByOrganizationIdAsync(Guid organizationId, bool includeDeleted = false);
        Task<Secret> GetByIdAsync(Guid id, bool includeDeleted = false);
        Task<Secret> CreateAsync(Secret secret);
        Task ReplaceAsync(Secret secret);
        Task SoftDeleteManyByIdAsync(IEnumerable<Guid> ids);
    }
}
