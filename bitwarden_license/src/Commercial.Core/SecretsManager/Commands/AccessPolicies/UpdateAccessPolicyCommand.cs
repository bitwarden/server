using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Commands.AccessPolicies.Interfaces;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;

namespace Bit.Commercial.Core.SecretsManager.Commands.AccessPolicies;

public class UpdateAccessPolicyCommand : IUpdateAccessPolicyCommand
{
    private readonly IAccessPolicyRepository _accessPolicyRepository;
    private readonly ICurrentContext _currentContext;
    private readonly IProjectRepository _projectRepository;
    private readonly IServiceAccountRepository _serviceAccountRepository;

    public UpdateAccessPolicyCommand(
        IAccessPolicyRepository accessPolicyRepository,
        ICurrentContext currentContext,
        IProjectRepository projectRepository,
        IServiceAccountRepository serviceAccountRepository)
    {
        _accessPolicyRepository = accessPolicyRepository;
        _currentContext = currentContext;
        _projectRepository = projectRepository;
        _serviceAccountRepository = serviceAccountRepository;
    }

    public async Task<BaseAccessPolicy> UpdateAsync(Guid id, bool read, bool write, Guid userId)
    {
        var accessPolicy = await _accessPolicyRepository.GetByIdAsync(id);
        if (accessPolicy == null)
        {
            throw new NotFoundException();
        }

        if (!await IsAllowedToUpdateAsync(accessPolicy, userId))
        {
            throw new NotFoundException();
        }

        accessPolicy.Read = read;
        accessPolicy.Write = write;
        accessPolicy.RevisionDate = DateTime.UtcNow;
        await _accessPolicyRepository.ReplaceAsync(accessPolicy);
        return accessPolicy;
    }

    private async Task<bool> IsAllowedToUpdateAsync(BaseAccessPolicy baseAccessPolicy, Guid userId) =>
        baseAccessPolicy switch
        {
            UserProjectAccessPolicy ap => await HasPermissionsAsync(ap.GrantedProject!.OrganizationId, userId,
                ap.GrantedProjectId),
            GroupProjectAccessPolicy ap => await HasPermissionsAsync(ap.GrantedProject!.OrganizationId, userId,
                ap.GrantedProjectId),
            ServiceAccountProjectAccessPolicy ap => await HasPermissionsAsync(ap.GrantedProject!.OrganizationId,
                userId, ap.GrantedProjectId),
            UserServiceAccountAccessPolicy ap => await HasPermissionsAsync(
                ap.GrantedServiceAccount!.OrganizationId,
                userId, serviceAccountIdToCheck: ap.GrantedServiceAccountId),
            GroupServiceAccountAccessPolicy ap => await HasPermissionsAsync(
                ap.GrantedServiceAccount!.OrganizationId,
                userId, serviceAccountIdToCheck: ap.GrantedServiceAccountId),
            _ => throw new ArgumentException("Unsupported access policy type provided."),
        };

    private async Task<bool> HasPermissionsAsync(
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
                if (projectIdToCheck.HasValue)
                {
                    hasAccess = await _projectRepository.UserHasWriteAccessToProject(projectIdToCheck.Value, userId);
                }
                else if (serviceAccountIdToCheck.HasValue)
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
