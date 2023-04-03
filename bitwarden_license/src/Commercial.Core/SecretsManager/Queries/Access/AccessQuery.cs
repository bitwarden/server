using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.SecretsManager.Models.Data;
using Bit.Core.SecretsManager.Queries.Access.Interfaces;
using Bit.Core.SecretsManager.Repositories;

namespace Bit.Commercial.Core.SecretsManager.Queries.Access;

public class AccessQuery : IAccessQuery
{
    private readonly ICurrentContext _currentContext;
    private readonly IProjectRepository _projectRepository;
    private readonly ISecretRepository _secretRepository;
    private readonly IServiceAccountRepository _serviceAccountRepository;

    public AccessQuery(ICurrentContext currentContext, IProjectRepository projectRepository, ISecretRepository secretRepository, IServiceAccountRepository serviceAccountRepository)
    {
        _currentContext = currentContext;
        _projectRepository = projectRepository;
        _secretRepository = secretRepository;
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

        return accessCheck.OperationType switch
        {
            OperationType.RevokeAccessToken => await HasAccessToRevokeAccessTokenAsync(accessCheck, accessClient),
            OperationType.CreateAccessToken => await HasAccessToCreateAccessTokenAsync(accessCheck, accessClient),
            OperationType.CreateServiceAccount => HasAccessToCreateServiceAccount(accessClient),
            OperationType.UpdateServiceAccount => await HasAccessToUpdateServiceAccountAsync(accessCheck, accessClient),
            OperationType.CreateProject => HasAccessToCreateProject(accessClient),
            OperationType.UpdateProject => await HasAccessToUpdateProjectAsync(accessCheck, accessClient),
            _ => false,
        };
    }

    public async Task<bool> HasAccess(SecretAccessCheck secretAccessCheck)
    {
        if (!_currentContext.AccessSecretsManager(secretAccessCheck.OrganizationId))
        {
            return false;
        }

        var orgAdmin = await _currentContext.OrganizationAdmin(secretAccessCheck.OrganizationId);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.ClientType, orgAdmin);

        return secretAccessCheck.OperationType switch
        {
            OperationType.UpdateSecret => await HasAccessToUpdateSecretAsync(secretAccessCheck, accessClient),
            OperationType.CreateSecret => await HasAccessToCreateSecretAsync(secretAccessCheck, accessClient),
            _ => false,
        };
    }

    private async Task<bool> HasAccessToCreateSecretAsync(SecretAccessCheck accessCheck, AccessClientType accessClient)
    {
        var hasAccess = accessClient switch
        {
            AccessClientType.NoAccessCheck => true,
            AccessClientType.User => accessCheck.TargetProjectId != null && (await _projectRepository.AccessToProjectAsync(accessCheck.TargetProjectId.Value, accessCheck.UserId, accessClient)).Write,
            _ => false,
        };
        return hasAccess;
    }

    private async Task<bool> HasAccessToUpdateSecretAsync(SecretAccessCheck accessCheck, AccessClientType accessClient)
    {
        switch (accessClient)
        {
            case AccessClientType.NoAccessCheck:
                return true;
            case AccessClientType.User:
                var accessToOld = accessCheck.CurrentSecretId != null &&
                                  (await _secretRepository.AccessToSecretAsync(accessCheck.CurrentSecretId.Value,
                                      accessCheck.UserId, accessClient)).Write;
                var accessToNew = accessCheck.TargetProjectId != null &&
                                  (await _projectRepository.AccessToProjectAsync(accessCheck.TargetProjectId.Value,
                                      accessCheck.UserId, accessClient)).Write;
                return accessToOld && accessToNew;
            default:
                return false;
        }
    }

    private bool HasAccessToCreateProject(AccessClientType accessClient) =>
        accessClient != AccessClientType.ServiceAccount;

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

    private bool HasAccessToCreateServiceAccount(AccessClientType accessClient) =>
        accessClient != AccessClientType.ServiceAccount;

    private async Task<bool> HasAccessToUpdateServiceAccountAsync(AccessCheck accessCheck, AccessClientType accessClient)
    {
        if (accessClient == AccessClientType.ServiceAccount)
        {
            return false;
        }

        var hasAccess = accessClient switch
        {
            AccessClientType.NoAccessCheck => true,
            AccessClientType.User => await _serviceAccountRepository.UserHasWriteAccessToServiceAccount(accessCheck.TargetId, accessCheck.UserId),
            _ => false,
        };
        return hasAccess;
    }

    private async Task<bool> HasAccessToCreateAccessTokenAsync(AccessCheck accessCheck, AccessClientType accessClient)
    {
        if (accessClient == AccessClientType.ServiceAccount)
        {
            return false;
        }

        var hasAccess = accessClient switch
        {
            AccessClientType.NoAccessCheck => true,
            AccessClientType.User => await _serviceAccountRepository.UserHasWriteAccessToServiceAccount(
                accessCheck.TargetId, accessCheck.UserId),
            _ => false,
        };
        return hasAccess;
    }

    private async Task<bool> HasAccessToRevokeAccessTokenAsync(AccessCheck accessCheck, AccessClientType accessClient)
    {
        if (accessClient == AccessClientType.ServiceAccount)
        {
            return false;
        }


        var hasAccess = accessClient switch
        {
            AccessClientType.NoAccessCheck => true,
            AccessClientType.User => await _serviceAccountRepository.UserHasWriteAccessToServiceAccount(accessCheck.TargetId, accessCheck.UserId),
            _ => false,
        };

        return hasAccess;
    }

}
