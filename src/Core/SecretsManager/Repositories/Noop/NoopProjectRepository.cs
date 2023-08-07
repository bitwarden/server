using Bit.Core.Enums;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Models.Data;

namespace Bit.Core.SecretsManager.Repositories.Noop;

public class NoopProjectRepository : IProjectRepository
{
    public Task<IEnumerable<ProjectPermissionDetails>> GetManyByOrganizationIdAsync(Guid organizationId, Guid userId,
        AccessClientType accessType)
    {
        return Task.FromResult(null as IEnumerable<ProjectPermissionDetails>);
    }

    public Task<IEnumerable<Project>> GetManyByOrganizationIdWriteAccessAsync(Guid organizationId, Guid userId,
        AccessClientType accessType)
    {
        return Task.FromResult(null as IEnumerable<Project>);
    }

    public Task<IEnumerable<Project>> GetManyWithSecretsByIds(IEnumerable<Guid> ids)
    {
        return Task.FromResult(null as IEnumerable<Project>);
    }

    public Task<Project> GetByIdAsync(Guid id)
    {
        return Task.FromResult(null as Project);
    }

    public Task<Project> CreateAsync(Project project)
    {
        return Task.FromResult(null as Project);
    }

    public Task ReplaceAsync(Project project)
    {
        return Task.FromResult(0);
    }

    public Task DeleteManyByIdAsync(IEnumerable<Guid> ids)
    {
        return Task.FromResult(0);
    }

    public Task<IEnumerable<Project>> ImportAsync(IEnumerable<Project> projects)
    {
        return Task.FromResult(null as IEnumerable<Project>);
    }

    public Task<(bool Read, bool Write)> AccessToProjectAsync(Guid id, Guid userId, AccessClientType accessType)
    {
        return Task.FromResult((false, false));
    }

    public Task<bool> ProjectsAreInOrganization(List<Guid> projectIds, Guid organizationId)
    {
        return Task.FromResult(false);
    }

    public Task<int> GetProjectCountByOrganizationIdAsync(Guid organizationId)
    {
        return Task.FromResult(0);
    }
}
