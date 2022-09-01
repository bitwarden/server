using Bit.Core.Entities;

namespace Bit.Core.Repositories
{
    public interface IProjectRepository
    {
        Task<IEnumerable<Project>> GetManyByOrganizationIdAsync(Guid organizationId);
        Task<Project> GetByIdAsync(Guid id);
        Task<Project> CreateAsync(Project project);
        Task ReplaceAsync(Project project);
        Task SoftDeleteManyByIdAsync(IEnumerable<Guid> ids);
    }
}
