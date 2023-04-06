using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.SecretsManager.Enums;
using Bit.Core.SecretsManager.Models.Data;
using Bit.Core.SecretsManager.Queries.Access.Interfaces;
using Bit.Core.SecretsManager.Repositories;

namespace Bit.Commercial.Core.SecretsManager.Queries.Access;

public class SecretAccessQuery : ISecretAccessQuery
{
    private readonly ICurrentContext _currentContext;
    private readonly IProjectRepository _projectRepository;
    private readonly ISecretRepository _secretRepository;

    public SecretAccessQuery(ICurrentContext currentContext, IProjectRepository projectRepository, ISecretRepository secretRepository)
    {
        _currentContext = currentContext;
        _projectRepository = projectRepository;
        _secretRepository = secretRepository;
    }

    public async Task<bool> HasAccess(SecretAccessCheck secretAccessCheck)
    {
        if (!_currentContext.AccessSecretsManager(secretAccessCheck.OrganizationId))
        {
            return false;
        }

        var orgAdmin = await _currentContext.OrganizationAdmin(secretAccessCheck.OrganizationId);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.ClientType, orgAdmin);

        return secretAccessCheck.AccessOperationType switch
        {
            AccessOperationType.UpdateSecret => await HasAccessToUpdateSecretAsync(secretAccessCheck, accessClient),
            AccessOperationType.CreateSecret => await HasAccessToCreateSecretAsync(secretAccessCheck, accessClient),
            _ => false,
        };
    }

    private async Task<bool> HasAccessToCreateSecretAsync(SecretAccessCheck accessCheck, AccessClientType accessClient)
    {
        // FIXME can we clean this up?
        if (accessClient == AccessClientType.NoAccessCheck && accessCheck.TargetProjectId == null)
        {
            return true;
        }

        if (accessCheck.TargetProjectId == null)
        {
            return false;
        }

        var project = await _projectRepository.GetByIdAsync(accessCheck.TargetProjectId.Value);

        if (project == null || project.OrganizationId != accessCheck.OrganizationId)
        {
            return false;
        }

        return accessClient switch
        {
            AccessClientType.NoAccessCheck => true,
            AccessClientType.User => accessCheck.TargetProjectId != null && (await _projectRepository.AccessToProjectAsync(accessCheck.TargetProjectId.Value, accessCheck.UserId, accessClient)).Write,
            AccessClientType.ServiceAccount => false,
            _ => false,
        };
    }

    private async Task<bool> HasAccessToUpdateSecretAsync(SecretAccessCheck accessCheck, AccessClientType accessClient)
    {

        // FIXME can we clean this up? 
        if (accessClient == AccessClientType.NoAccessCheck && accessCheck.TargetProjectId == null)
        {
            return true;
        }

        if (accessCheck.TargetProjectId == null)
        {
            return false;
        }

        var project = await _projectRepository.GetByIdAsync(accessCheck.TargetProjectId.Value);

        if (project == null || project.OrganizationId != accessCheck.OrganizationId)
        {
            return false;
        }


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
}
