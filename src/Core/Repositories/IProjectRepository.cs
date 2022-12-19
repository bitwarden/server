using Bit.Core.Entities;
using Bit.Core.Identity;

namespace Bit.Core.Repositories;

public interface IProjectRepository
{
    Task<IEnumerable<Project>> GetManyByOrganizationIdAsync(Guid organizationId, Guid userId, ClientType clientType, bool checkAccess = true);
    Task<IEnumerable<Project>> GetManyByIds(IEnumerable<Guid> ids);
    Task<Project> GetByIdAsync(Guid id);
    Task<Project> CreateAsync(Project project);
    Task ReplaceAsync(Project project);
    Task DeleteManyByIdAsync(IEnumerable<Guid> ids);
}
