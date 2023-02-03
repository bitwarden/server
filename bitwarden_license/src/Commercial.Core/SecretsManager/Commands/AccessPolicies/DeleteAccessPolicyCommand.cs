using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Commands.AccessPolicies.Interfaces;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;

namespace Bit.Commercial.Core.SecretsManager.Commands.AccessPolicies;

public class DeleteAccessPolicyCommand : IDeleteAccessPolicyCommand
{
    private readonly IAccessPolicyRepository _accessPolicyRepository;
    private readonly ICurrentContext _currentContext;
    private readonly IProjectRepository _projectRepository;
    private readonly IServiceAccountRepository _serviceAccountRepository;

    public DeleteAccessPolicyCommand(
        IAccessPolicyRepository accessPolicyRepository,
        ICurrentContext currentContext,
        IProjectRepository projectRepository,
        IServiceAccountRepository serviceAccountRepository)
    {
        _projectRepository = projectRepository;
        _serviceAccountRepository = serviceAccountRepository;
        _accessPolicyRepository = accessPolicyRepository;
        _currentContext = currentContext;
    }

    public async Task DeleteAsync(Guid id, Guid userId)
    {
        var accessPolicy = await _accessPolicyRepository.GetByIdAsync(id);
        if (accessPolicy == null)
        {
            throw new NotFoundException();
        }

        if (!await IsAllowedToDeleteAsync(accessPolicy, userId))
        {
            throw new NotFoundException();
        }

        await _accessPolicyRepository.DeleteAsync(id);
    }

    private async Task<bool> IsAllowedToDeleteAsync(BaseAccessPolicy baseAccessPolicy, Guid userId) =>
        baseAccessPolicy switch
        {
            UserProjectAccessPolicy ap => await HasPermissionAsync(ap.GrantedProject!.OrganizationId, userId,
                ap.GrantedProjectId),
            GroupProjectAccessPolicy ap => await HasPermissionAsync(ap.GrantedProject!.OrganizationId, userId,
                ap.GrantedProjectId),
            ServiceAccountProjectAccessPolicy ap => await HasPermissionAsync(ap.GrantedProject!.OrganizationId,
                userId, ap.GrantedProjectId),
            UserServiceAccountAccessPolicy ap => await HasPermissionAsync(
                ap.GrantedServiceAccount!.OrganizationId,
                userId, serviceAccountIdToCheck: ap.GrantedServiceAccountId),
            GroupServiceAccountAccessPolicy ap => await HasPermissionAsync(
                ap.GrantedServiceAccount!.OrganizationId,
                userId, serviceAccountIdToCheck: ap.GrantedServiceAccountId),
            _ => throw new ArgumentException("Unsupported access policy type provided."),
        };

    private async Task<bool> HasPermissionAsync(
        Guid organizationId,
        Guid userId,
        Guid? projectIdToCheck = null,
        Guid? serviceAccountIdToCheck = null)
    {
        if (!_currentContext.AccessSecretsManager(organizationId))
        {
            return false;
        }

        var orgAdmin = await _currentContext.OrganizationAdmin(organizationId);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.ClientType, orgAdmin);

        bool hasAccess;
        switch (accessClient)
        {
            case AccessClientType.NoAccessCheck:
                hasAccess = true;
                break;
            case AccessClientType.User:
                if (projectIdToCheck != null)
                {
                    hasAccess = await _projectRepository.UserHasWriteAccessToProject(projectIdToCheck.Value, userId);
                }
                else if (serviceAccountIdToCheck != null)
                {
                    hasAccess =
                        await _serviceAccountRepository.UserHasWriteAccessToServiceAccount(
                            serviceAccountIdToCheck.Value, userId);
                }
                else
                {
                    throw new ArgumentException("No ID to check provided.");
                }

                break;
            default:
                hasAccess = false;
                break;
        }

        return hasAccess;
    }
}
