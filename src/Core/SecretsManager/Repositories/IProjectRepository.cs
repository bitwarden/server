using Bit.Core.Enums;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Models.Data;

namespace Bit.Core.SecretsManager.Repositories;

public interface IProjectRepository
{
    Task<IEnumerable<Project>> GetManyByOrganizationIdAsync(Guid organizationId, Guid userId, AccessClientType accessType);
    Task<IEnumerable<Project>> GetManyByOrganizationIdWriteAccessAsync(Guid organizationId, Guid userId, AccessClientType accessType);
    Task<IEnumerable<Project>> GetManyByIds(IEnumerable<Guid> ids);
    Task<ProjectPermissionDetails> GetPermissionDetailsByIdAsync(Guid id, Guid userId);
    Task<Project> GetByIdAsync(Guid id);
    Task<Project> CreateAsync(Project project);
    Task ReplaceAsync(Project project);
    Task DeleteManyByIdAsync(IEnumerable<Guid> ids);
    Task<IEnumerable<Project>> ImportAsync(IEnumerable<Project> projects);
    Task<bool> UserHasReadAccessToProject(Guid id, Guid userId);
    Task<bool> UserHasWriteAccessToProject(Guid id, Guid userId);
    Task<bool> ServiceAccountHasWriteAccessToProject(Guid id, Guid userId);
    Task<bool> ServiceAccountHasReadAccessToProject(Guid id, Guid userId);
}
