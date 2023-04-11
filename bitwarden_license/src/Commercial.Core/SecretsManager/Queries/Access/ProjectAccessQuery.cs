using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.SecretsManager.Models.Data;
using Bit.Core.SecretsManager.Queries.Access.Interfaces;
using Bit.Core.SecretsManager.Repositories;

namespace Bit.Commercial.Core.SecretsManager.Queries.Access;

public class ProjectAccessQuery : IProjectAccessQuery
{
    private readonly ICurrentContext _currentContext;
    private readonly IProjectRepository _projectRepository;

    public ProjectAccessQuery(ICurrentContext currentContext, IProjectRepository projectRepository)
    {
        _currentContext = currentContext;
        _projectRepository = projectRepository;
    }

    public async Task<bool> HasAccessToCreateAsync(AccessCheck accessCheck)
    {
        if (!_currentContext.AccessSecretsManager(accessCheck.OrganizationId))
        {
            return false;
        }

        var accessClient = await GetAccessClientAsync(accessCheck);
        return accessClient switch
        {
            AccessClientType.NoAccessCheck => true,
            AccessClientType.User => true,
            AccessClientType.ServiceAccount => false,
            _ => false,
        };
    }

    public async Task<bool> HasAccessToUpdateAsync(AccessCheck accessCheck)
    {
        if (!_currentContext.AccessSecretsManager(accessCheck.OrganizationId))
        {
            return false;
        }

        var accessClient = await GetAccessClientAsync(accessCheck);
        if (accessClient == AccessClientType.ServiceAccount)
        {
            return false;
        }

        var access =
            await _projectRepository.AccessToProjectAsync(accessCheck.TargetId, accessCheck.UserId,
                accessClient);
        return access.Write;
    }

    private async Task<AccessClientType> GetAccessClientAsync(AccessCheck accessCheck)
    {
        var orgAdmin = await _currentContext.OrganizationAdmin(accessCheck.OrganizationId);
        return AccessClientHelper.ToAccessClient(_currentContext.ClientType, orgAdmin);
    }
}
