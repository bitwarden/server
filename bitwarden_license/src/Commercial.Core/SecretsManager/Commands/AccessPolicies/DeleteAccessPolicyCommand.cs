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

        switch (accessPolicy)
        {
            case UserProjectAccessPolicy ap:
                if (ap.GrantedProjectId == null)
                {
                    throw new BadRequestException();
                }

                var project = await _projectRepository.GetByIdAsync(ap.GrantedProjectId.Value);
                await DeleteProjectGrantAsync(ap.Id, userId, project.OrganizationId, project.Id);
                break;
            case GroupProjectAccessPolicy ap:
                await DeleteProjectGrantAsync(ap.Id, userId, ap.Group?.OrganizationId, ap.GrantedProjectId);
                break;
            case ServiceAccountProjectAccessPolicy ap:
                await DeleteProjectGrantAsync(ap.Id, userId, ap.ServiceAccount?.OrganizationId, ap.GrantedProjectId);
                break;
            case UserServiceAccountAccessPolicy ap:
                await DeleteServiceAccountGrantAsync(ap.Id, userId, ap.GrantedServiceAccountId);
                break;
            case GroupServiceAccountAccessPolicy ap:
                await DeleteServiceAccountGrantAsync(ap.Id, userId, ap.GrantedServiceAccountId);
                break;
            default:
                throw new ArgumentException("Unsupported access policy type provided.");
        }
    }

    private async Task DeleteProjectGrantAsync(Guid id, Guid userId, Guid? organizationId, Guid? projectId)
    {
        if (organizationId == null || projectId == null)
        {
            throw new BadRequestException();
        }

        await CheckPermissionsAsync(organizationId.Value, userId, projectIdToCheck: projectId.Value);
        await _accessPolicyRepository.DeleteAsync(id);
    }

    private async Task DeleteServiceAccountGrantAsync(Guid id, Guid userId, Guid? serviceAccountId)
    {
        if (serviceAccountId == null)
        {
            throw new BadRequestException();
        }

        var serviceAccount = await _serviceAccountRepository.GetByIdAsync(serviceAccountId.Value);
        await CheckPermissionsAsync(serviceAccount.OrganizationId, userId, serviceAccountIdToCheck: serviceAccount.Id);
        await _accessPolicyRepository.DeleteAsync(id);
    }

    private async Task CheckPermissionsAsync(
        Guid organizationId,
        Guid userId,
        Guid? projectIdToCheck = null,
        Guid? serviceAccountIdToCheck = null)
    {
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
            throw new UnauthorizedAccessException();
        }
    }
}
