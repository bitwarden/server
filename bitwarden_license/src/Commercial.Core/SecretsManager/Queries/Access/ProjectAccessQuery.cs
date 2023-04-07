using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.SecretsManager.Enums;
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

    public async Task<bool> HasAccess(AccessCheck accessCheck)
    {
        if (!_currentContext.AccessSecretsManager(accessCheck.OrganizationId))
        {
            return false;
        }

        var orgAdmin = await _currentContext.OrganizationAdmin(accessCheck.OrganizationId);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.ClientType, orgAdmin);

        return accessCheck.AccessOperationType switch
        {
            AccessOperationType.CreateProject => HasAccessToCreateProject(accessClient),
            AccessOperationType.UpdateProject => await HasAccessToUpdateProjectAsync(accessCheck, accessClient),
            _ => false,
        };
    }

    private bool HasAccessToCreateProject(AccessClientType accessClient)
    {
        return accessClient switch
        {
            AccessClientType.NoAccessCheck => true,
            AccessClientType.User => true,
            AccessClientType.ServiceAccount => false,
            _ => false,
        };
    }

    private async Task<bool> HasAccessToUpdateProjectAsync(AccessCheck accessCheck, AccessClientType accessClient)
    {
        if (accessClient == AccessClientType.ServiceAccount)
        {
            return false;
        }

        var access =
            await _projectRepository.AccessToProjectAsync(accessCheck.TargetId, accessCheck.UserId,
                accessClient);
        return access.Write;
    }
}
