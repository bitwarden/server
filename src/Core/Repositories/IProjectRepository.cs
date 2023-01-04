using Bit.Core.Entities;

namespace Bit.Core.Repositories;

public interface IProjectRepository
{
    Task<IEnumerable<Project>> GetManyByOrganizationIdAsync(Guid organizationId, Guid userId);
    Task<IEnumerable<Project>> GetManyByIds(IEnumerable<Guid> ids);
    Task<Project> GetByIdAsync(Guid id);
    Task<Project> CreateAsync(Project project);
    Task ReplaceAsync(Project project);
    Task DeleteManyByIdAsync(IEnumerable<Guid> ids);
}
