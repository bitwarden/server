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

        switch (accessPolicy)
        {
            case UserProjectAccessPolicy ap:
                if (ap.GrantedProject == null)
                {
                    throw new NotFoundException();
                }

                await CheckPermissionsAsync(ap.GrantedProject.OrganizationId, userId, ap.GrantedProjectId);
                return await UpdateAccessPolicyAsync(ap, read, write);
            case GroupProjectAccessPolicy ap:
                if (ap.GrantedProject == null)
                {
                    throw new NotFoundException();
                }

                await CheckPermissionsAsync(ap.GrantedProject.OrganizationId, userId, ap.GrantedProjectId);
                return await UpdateAccessPolicyAsync(ap, read, write);
            case ServiceAccountProjectAccessPolicy ap:
                if (ap.GrantedProject == null)
                {
                    throw new NotFoundException();
                }

                await CheckPermissionsAsync(ap.GrantedProject.OrganizationId, userId, ap.GrantedProjectId);
                return await UpdateAccessPolicyAsync(ap, read, write);
            case UserServiceAccountAccessPolicy ap:
                if (ap.GrantedServiceAccount == null)
                {
                    throw new NotFoundException();
                }

                await CheckPermissionsAsync(ap.GrantedServiceAccount.OrganizationId, userId,
                    serviceAccountIdToCheck: ap.GrantedServiceAccountId);
                return await UpdateAccessPolicyAsync(ap, read, write);
            case GroupServiceAccountAccessPolicy ap:
                if (ap.GrantedServiceAccount == null)
                {
                    throw new NotFoundException();
                }

                await CheckPermissionsAsync(ap.GrantedServiceAccount.OrganizationId, userId,
                    serviceAccountIdToCheck: ap.GrantedServiceAccountId);
                return await UpdateAccessPolicyAsync(ap, read, write);
            default:
                throw new ArgumentException("Unsupported access policy type provided.", nameof(accessPolicy));
        }
    }

    private async Task CheckPermissionsAsync(
        Guid organizationId,
        Guid userId,
        Guid? projectIdToCheck = null,
        Guid? serviceAccountIdToCheck = null)
    {
        if (!_currentContext.AccessSecretsManager(organizationId))
        {
            throw new NotFoundException();
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

        if (!hasAccess)
        {
            throw new NotFoundException();
        }
    }

    private async Task<BaseAccessPolicy> UpdateAccessPolicyAsync(BaseAccessPolicy accessPolicy, bool read, bool write)
    {
        accessPolicy.Read = read;
        accessPolicy.Write = write;
        accessPolicy.RevisionDate = DateTime.UtcNow;
        await _accessPolicyRepository.ReplaceAsync(accessPolicy);
        return accessPolicy;
    }
}
