using Bit.Core.Enums;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Models.Data;

namespace Bit.Core.SecretsManager.Repositories;

public interface IProjectRepository
{
    Task<IEnumerable<ProjectPermissionDetails>> GetManyByOrganizationIdAsync(Guid organizationId, Guid userId, AccessClientType accessType);
    Task<IEnumerable<Project>> GetManyByOrganizationIdWriteAccessAsync(Guid organizationId, Guid userId, AccessClientType accessType);
    Task<IEnumerable<Project>> GetManyWithSecretsByIds(IEnumerable<Guid> ids);
    Task<Project> GetByIdAsync(Guid id);
    Task<Project> CreateAsync(Project project);
    Task ReplaceAsync(Project project);
    Task DeleteManyByIdAsync(IEnumerable<Guid> ids);
    Task<IEnumerable<Project>> ImportAsync(IEnumerable<Project> projects);
    Task<(bool Read, bool Write)> AccessToProjectAsync(Guid id, Guid userId, AccessClientType accessType);
    Task<bool> ProjectsAreInOrganization(List<Guid> projectIds, Guid organizationId);
    Task<int> GetProjectCountByOrganizationIdAsync(Guid organizationId);
}
