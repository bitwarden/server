using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.AuthorizationRequirements;
using Bit.Core.SecretsManager.Models.Data;
using Bit.Core.SecretsManager.Queries.Interfaces;
using Bit.Core.SecretsManager.Repositories;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Commercial.Core.SecretsManager.AuthorizationHandlers.AccessPolicies;

public class
    PeopleAccessPoliciesAuthorizationHandler : AuthorizationHandler<PeopleAccessPoliciesOperationRequirement,
        PeopleAccessPolicies>
{
    private readonly IAccessClientQuery _accessClientQuery;
    private readonly ICurrentContext _currentContext;
    private readonly IGroupRepository _groupRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IProjectRepository _projectRepository;

    public PeopleAccessPoliciesAuthorizationHandler(ICurrentContext currentContext,
        IAccessClientQuery accessClientQuery,
        IGroupRepository groupRepository,
        IOrganizationUserRepository organizationUserRepository,
        IProjectRepository projectRepository)
    {
        _currentContext = currentContext;
        _accessClientQuery = accessClientQuery;
        _groupRepository = groupRepository;
        _organizationUserRepository = organizationUserRepository;
        _projectRepository = projectRepository;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        PeopleAccessPoliciesOperationRequirement requirement,
        PeopleAccessPolicies resource)
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
            case not null when requirement == PeopleAccessPoliciesOperations.ReplaceProjectPeople:
                await CanReplaceProjectPeopleAsync(context, requirement, resource, accessClient, userId);
                break;
            default:
                throw new ArgumentException("Unsupported operation requirement type provided.",
                    nameof(requirement));
        }
    }

    private async Task CanReplaceProjectPeopleAsync(AuthorizationHandlerContext context,
        PeopleAccessPoliciesOperationRequirement requirement, PeopleAccessPolicies resource,
        AccessClientType accessClient, Guid userId)
    {
        var access = await _projectRepository.AccessToProjectAsync(resource.Id, userId, accessClient);
        if (access.Write)
        {
            if (resource.UserAccessPolicies != null && resource.UserAccessPolicies.Any())
            {
                var orgUserIds = resource.UserAccessPolicies.Select(ap => ap.OrganizationUserId!.Value).ToList();
                var users = await _organizationUserRepository.GetManyAsync(orgUserIds);
                if (users.Any(user => user.OrganizationId != resource.OrganizationId) ||
                    users.Count != orgUserIds.Count)
                {
                    return;
                }
            }

            if (resource.GroupAccessPolicies != null && resource.GroupAccessPolicies.Any())
            {
                var groupIds = resource.GroupAccessPolicies.Select(ap => ap.GroupId!.Value).ToList();
                var groups = await _groupRepository.GetManyByManyIds(groupIds);
                if (groups.Any(group => group.OrganizationId != resource.OrganizationId) ||
                    groups.Count != groupIds.Count)
                {
                    return;
                }
            }

            context.Succeed(requirement);
        }
    }
}
