using Bit.Core.Entities;

namespace Bit.Core.Repositories
{
    public interface IProjectSecretsRepository
    {
        Task<IEnumerable<ProjectSecrets>> GetManyByOrganizationIdAsync(Guid organizationId);
        Task<ProjectSecrets> GetByIdAsync(Guid id);
        Task<ProjectSecrets> CreateAsync(ProjectSecrets projectSecrets);
        Task ReplaceAsync(ProjectSecrets projectSecrets);
        Task SoftDeleteManyByIdAsync(IEnumerable<Guid> ids);
    }
}
