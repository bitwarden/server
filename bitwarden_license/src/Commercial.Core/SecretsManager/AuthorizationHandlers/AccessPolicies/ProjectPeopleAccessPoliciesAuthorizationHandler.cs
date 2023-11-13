using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.SecretsManager.AuthorizationRequirements;
using Bit.Core.SecretsManager.Models.Data;
using Bit.Core.SecretsManager.Queries.AccessPolicies.Interfaces;
using Bit.Core.SecretsManager.Queries.Interfaces;
using Bit.Core.SecretsManager.Repositories;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Commercial.Core.SecretsManager.AuthorizationHandlers.AccessPolicies;

public class
    ProjectPeopleAccessPoliciesAuthorizationHandler : AuthorizationHandler<
        ProjectPeopleAccessPoliciesOperationRequirement,
        ProjectPeopleAccessPolicies>
{
    private readonly IAccessClientQuery _accessClientQuery;
    private readonly ICurrentContext _currentContext;
    private readonly IProjectRepository _projectRepository;
    private readonly ISameOrganizationQuery _sameOrganizationQuery;

    public ProjectPeopleAccessPoliciesAuthorizationHandler(ICurrentContext currentContext,
        IAccessClientQuery accessClientQuery,
        ISameOrganizationQuery sameOrganizationQuery,
        IProjectRepository projectRepository)
    {
        _currentContext = currentContext;
        _accessClientQuery = accessClientQuery;
        _sameOrganizationQuery = sameOrganizationQuery;
        _projectRepository = projectRepository;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        ProjectPeopleAccessPoliciesOperationRequirement requirement,
        ProjectPeopleAccessPolicies resource)
    {
        if (!_currentContext.AccessSecretsManager(resource.OrganizationId))
        {
            return;
        }

        // Only users and admins should be able to manipulate access policies
        var (accessClient, userId) =
            await _accessClientQuery.GetAccessClientAsync(context.User, resource.OrganizationId);
        if (accessClient != AccessClientType.User && accessClient != AccessClientType.NoAccessCheck)
        {
            return;
        }

        switch (requirement)
        {
            case not null when requirement == ProjectPeopleAccessPoliciesOperations.Replace:
                await CanReplaceProjectPeopleAsync(context, requirement, resource, accessClient, userId);
                break;
            default:
                throw new ArgumentException("Unsupported operation requirement type provided.",
                    nameof(requirement));
        }
    }

    private async Task CanReplaceProjectPeopleAsync(AuthorizationHandlerContext context,
        ProjectPeopleAccessPoliciesOperationRequirement requirement, ProjectPeopleAccessPolicies resource,
        AccessClientType accessClient, Guid userId)
    {
        var access = await _projectRepository.AccessToProjectAsync(resource.Id, userId, accessClient);
        if (access.Write)
        {
            if (resource.UserAccessPolicies != null && resource.UserAccessPolicies.Any())
            {
                var orgUserIds = resource.UserAccessPolicies.Select(ap => ap.OrganizationUserId!.Value).ToList();
                if (!await _sameOrganizationQuery.OrgUsersInTheSameOrgAsync(orgUserIds, resource.OrganizationId))
                {
                    return;
                }
            }

            if (resource.GroupAccessPolicies != null && resource.GroupAccessPolicies.Any())
            {
                var groupIds = resource.GroupAccessPolicies.Select(ap => ap.GroupId!.Value).ToList();
                if (!await _sameOrganizationQuery.GroupsInTheSameOrgAsync(groupIds, resource.OrganizationId))
                {
                    return;
                }
            }

            context.Succeed(requirement);
        }
    }
}
