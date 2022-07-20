using Bit.Core.Entities;

namespace Bit.Core.Repositories
{
    public interface ISecretRepository
    {
        Task<IEnumerable<Secret>> GetManyByOrganizationIdAsync(Guid organizationId);
        Task<Secret> GetByIdAsync(Guid id);
        Task<Secret> CreateAsync(Secret obj);
        Task ReplaceAsync(Secret obj);
        Task SoftDeleteManyByIdAsync(IEnumerable<Guid> ids);
    }
}
