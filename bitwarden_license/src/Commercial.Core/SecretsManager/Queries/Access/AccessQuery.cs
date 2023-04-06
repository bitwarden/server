using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.SecretsManager.Enums;
using Bit.Core.SecretsManager.Models.Data;
using Bit.Core.SecretsManager.Queries.Access.Interfaces;
using Bit.Core.SecretsManager.Repositories;

namespace Bit.Commercial.Core.SecretsManager.Queries.Access;

public class AccessQuery : IAccessQuery
{
    private readonly ICurrentContext _currentContext;
    private readonly IProjectRepository _projectRepository;
    private readonly IServiceAccountRepository _serviceAccountRepository;

    public AccessQuery(ICurrentContext currentContext, IProjectRepository projectRepository, IServiceAccountRepository serviceAccountRepository)
    {
        _currentContext = currentContext;
        _projectRepository = projectRepository;
        _serviceAccountRepository = serviceAccountRepository;
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
            AccessOperationType.RevokeAccessToken => await HasAccessToRevokeAccessTokenAsync(accessCheck, accessClient),
            AccessOperationType.CreateAccessToken => await HasAccessToCreateAccessTokenAsync(accessCheck, accessClient),
            AccessOperationType.CreateServiceAccount => HasAccessToCreateServiceAccount(accessClient),
            AccessOperationType.UpdateServiceAccount => await HasAccessToUpdateServiceAccountAsync(accessCheck, accessClient),
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

    private bool HasAccessToCreateServiceAccount(AccessClientType accessClient)
    {
        return accessClient switch
        {
            AccessClientType.NoAccessCheck => true,
            AccessClientType.User => true,
            AccessClientType.ServiceAccount => false,
            _ => false,
        };
    }

    private async Task<bool> HasAccessToUpdateServiceAccountAsync(AccessCheck accessCheck,
        AccessClientType accessClient)
    {
        var access =
            await _serviceAccountRepository.AccessToServiceAccountAsync(accessCheck.TargetId, accessCheck.UserId,
                accessClient);
        return access.Write;
    }

    private async Task<bool> HasAccessToCreateAccessTokenAsync(AccessCheck accessCheck, AccessClientType accessClient)
    {
        var access =
            await _serviceAccountRepository.AccessToServiceAccountAsync(accessCheck.TargetId, accessCheck.UserId,
                accessClient);
        return access.Write;
    }

    private async Task<bool> HasAccessToRevokeAccessTokenAsync(AccessCheck accessCheck, AccessClientType accessClient)
    {
        var access =
            await _serviceAccountRepository.AccessToServiceAccountAsync(accessCheck.TargetId, accessCheck.UserId,
                accessClient);
        return access.Write;
    }
}
