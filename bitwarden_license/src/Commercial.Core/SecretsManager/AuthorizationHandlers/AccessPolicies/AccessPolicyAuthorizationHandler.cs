using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.AuthorizationRequirements;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Commercial.Core.SecretsManager.AuthorizationHandlers.AccessPolicies;

public class AccessPolicyAuthorizationHandler : AuthorizationHandler<AccessPolicyOperationRequirement, BaseAccessPolicy>
{
    private readonly ICurrentContext _currentContext;
    private readonly IGroupRepository _groupRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IServiceAccountRepository _serviceAccountRepository;
    private readonly IUserService _userService;

    public AccessPolicyAuthorizationHandler(ICurrentContext currentContext, IUserService userService,
        IProjectRepository projectRepository, IServiceAccountRepository serviceAccountRepository,
        IOrganizationUserRepository organizationUserRepository, IGroupRepository groupRepository)
    {
        _currentContext = currentContext;
        _userService = userService;
        _organizationUserRepository = organizationUserRepository;
        _projectRepository = projectRepository;
        _groupRepository = groupRepository;
        _serviceAccountRepository = serviceAccountRepository;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        AccessPolicyOperationRequirement requirement,
        BaseAccessPolicy resource)
    {
        switch (requirement)
        {
            case not null when requirement == AccessPolicyOperations.Create:
                await CanCreateAccessPolicyAsync(context, requirement, resource);
                break;
            case not null when requirement == AccessPolicyOperations.Update:
                await CanUpdateAccessPolicyAsync(context, requirement, resource);
                break;
            case not null when requirement == AccessPolicyOperations.Delete:
                await CanDeleteAccessPolicyAsync(context, requirement, resource);
                break;
            default:
                throw new ArgumentException("Unsupported project operation requirement type provided.",
                    nameof(requirement));
        }
    }

    private async Task CanCreateAccessPolicyAsync(AuthorizationHandlerContext context,
        AccessPolicyOperationRequirement requirement, BaseAccessPolicy resource)
    {
        switch (resource)
        {
            case UserProjectAccessPolicy ap:
                await CanCreateAsync(context, requirement, ap);
                break;
            case GroupProjectAccessPolicy ap:
                await CanCreateAsync(context, requirement, ap);
                break;
            case ServiceAccountProjectAccessPolicy ap:
                await CanCreateAsync(context, requirement, ap);
                break;
            case UserServiceAccountAccessPolicy ap:
                await CanCreateAsync(context, requirement, ap);
                break;
            case GroupServiceAccountAccessPolicy ap:
                await CanCreateAsync(context, requirement, ap);
                break;
            default:
                throw new ArgumentException("Unsupported access policy type provided.");
        }
    }

    private async Task CanUpdateAccessPolicyAsync(AuthorizationHandlerContext context,
        AccessPolicyOperationRequirement requirement, BaseAccessPolicy resource)
    {
        var access = await GetAccessPolicyAccessAsync(context, resource);

        if (access.Write)
        {
            context.Succeed(requirement);
        }
    }

    private async Task CanDeleteAccessPolicyAsync(AuthorizationHandlerContext context,
        AccessPolicyOperationRequirement requirement, BaseAccessPolicy resource)
    {
        var access = await GetAccessPolicyAccessAsync(context, resource);

        if (access.Write)
        {
            context.Succeed(requirement);
        }
    }

    private async Task CanCreateAsync(AuthorizationHandlerContext context,
        AccessPolicyOperationRequirement requirement, UserProjectAccessPolicy resource)
    {
        var user = await _organizationUserRepository.GetByIdAsync(resource.OrganizationUserId!.Value);
        if (user.OrganizationId != resource.GrantedProject?.OrganizationId)
        {
            return;
        }

        var access = await GetAccessAsync(context, resource.GrantedProject!.OrganizationId, resource.GrantedProjectId);

        if (access.Write)
        {
            context.Succeed(requirement);
        }
    }

    private async Task CanCreateAsync(AuthorizationHandlerContext context,
        AccessPolicyOperationRequirement requirement, GroupProjectAccessPolicy resource)
    {
        var group = await _groupRepository.GetByIdAsync(resource.GroupId!.Value);
        if (group.OrganizationId != resource.GrantedProject?.OrganizationId)
        {
            return;
        }

        var access = await GetAccessAsync(context, resource.GrantedProject!.OrganizationId, resource.GrantedProjectId);

        if (access.Write)
        {
            context.Succeed(requirement);
        }
    }

    private async Task CanCreateAsync(AuthorizationHandlerContext context,
        AccessPolicyOperationRequirement requirement, ServiceAccountProjectAccessPolicy resource)
    {
        var projectOrganizationId = resource.GrantedProject?.OrganizationId;
        var serviceAccountOrgId = resource.ServiceAccount?.OrganizationId;

        if (projectOrganizationId == null && resource.GrantedProjectId.HasValue)
        {
            var project = await _projectRepository.GetByIdAsync(resource.GrantedProjectId.Value);
            projectOrganizationId = project?.OrganizationId;
        }

        if (serviceAccountOrgId == null && resource.ServiceAccountId.HasValue)
        {
            var serviceAccount = await _serviceAccountRepository.GetByIdAsync(resource.ServiceAccountId.Value);
            serviceAccountOrgId = serviceAccount?.OrganizationId;
        }

        if (!serviceAccountOrgId.HasValue || !projectOrganizationId.HasValue || serviceAccountOrgId != projectOrganizationId)
        {
            return;
        }

        var access = await GetAccessAsync(context, projectOrganizationId.Value, resource.GrantedProjectId,
            resource.ServiceAccountId);

        if (access.Write)
        {
            context.Succeed(requirement);
        }
    }

    private async Task CanCreateAsync(AuthorizationHandlerContext context,
        AccessPolicyOperationRequirement requirement, UserServiceAccountAccessPolicy resource)
    {
        var user = await _organizationUserRepository.GetByIdAsync(resource.OrganizationUserId!.Value);
        if (user.OrganizationId != resource.GrantedServiceAccount!.OrganizationId)
        {
            return;
        }

        var access = await GetAccessAsync(context, resource.GrantedServiceAccount!.OrganizationId,
            serviceAccountIdToCheck: resource.GrantedServiceAccountId);

        if (access.Write)
        {
            context.Succeed(requirement);
        }
    }

    private async Task CanCreateAsync(AuthorizationHandlerContext context,
        AccessPolicyOperationRequirement requirement, GroupServiceAccountAccessPolicy resource)
    {
        var group = await _groupRepository.GetByIdAsync(resource.GroupId!.Value);
        if (group.OrganizationId != resource.GrantedServiceAccount!.OrganizationId)
        {
            return;
        }

        var access = await GetAccessAsync(context, resource.GrantedServiceAccount!.OrganizationId,
            serviceAccountIdToCheck: resource.GrantedServiceAccountId);

        if (access.Write)
        {
            context.Succeed(requirement);
        }
    }

    private async Task<(bool Read, bool Write)> GetAccessPolicyAccessAsync(AuthorizationHandlerContext context,
        BaseAccessPolicy resource) =>
        resource switch
        {
            UserProjectAccessPolicy ap => await GetAccessAsync(context, ap.GrantedProject!.OrganizationId,
                ap.GrantedProjectId),
            GroupProjectAccessPolicy ap => await GetAccessAsync(context, ap.GrantedProject!.OrganizationId,
                ap.GrantedProjectId),
            ServiceAccountProjectAccessPolicy ap => await GetAccessAsync(context, ap.GrantedProject!.OrganizationId,
                ap.GrantedProjectId, ap.ServiceAccountId),
            UserServiceAccountAccessPolicy ap => await GetAccessAsync(context, ap.GrantedServiceAccount!.OrganizationId,
                serviceAccountIdToCheck: ap.GrantedServiceAccountId),
            GroupServiceAccountAccessPolicy ap => await GetAccessAsync(context,
                ap.GrantedServiceAccount!.OrganizationId, serviceAccountIdToCheck: ap.GrantedServiceAccountId),
            _ => throw new ArgumentException("Unsupported access policy type provided."),
        };

    private async Task<(bool Read, bool Write)> GetAccessAsync(AuthorizationHandlerContext context,
        Guid organizationId, Guid? projectIdToCheck = null,
        Guid? serviceAccountIdToCheck = null)
    {
        if (!_currentContext.AccessSecretsManager(organizationId))
        {
            return (false, false);
        }

        var (accessClient, userId) = await GetAccessClientAsync(context, organizationId);

        if (accessClient is AccessClientType.ServiceAccount or AccessClientType.Organization)
        {
            return (false, false);
        }

        if (projectIdToCheck.HasValue && serviceAccountIdToCheck.HasValue)
        {
            var projectAccess =
                await _projectRepository.AccessToProjectAsync(projectIdToCheck.Value, userId, accessClient);
            var serviceAccountAccess =
                await _serviceAccountRepository.AccessToServiceAccountAsync(serviceAccountIdToCheck.Value, userId,
                    accessClient);
            return (
                projectAccess.Read && serviceAccountAccess.Read,
                projectAccess.Write && serviceAccountAccess.Write);
        }

        if (projectIdToCheck.HasValue)
        {
            return await _projectRepository.AccessToProjectAsync(projectIdToCheck.Value, userId, accessClient);
        }

        if (serviceAccountIdToCheck.HasValue)
        {
            return await _serviceAccountRepository.AccessToServiceAccountAsync(serviceAccountIdToCheck.Value, userId,
                accessClient);
        }

        throw new ArgumentException("No ID to check provided.");
    }

    private async Task<(AccessClientType AccessClientType, Guid UserId)> GetAccessClientAsync(
        AuthorizationHandlerContext context, Guid organizationId)
    {
        var orgAdmin = await _currentContext.OrganizationAdmin(organizationId);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.ClientType, orgAdmin);
        var userId = _userService.GetProperUserId(context.User).Value;
        return (accessClient, userId);
    }
}
