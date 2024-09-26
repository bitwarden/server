#nullable enable
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.SecretsManager.AuthorizationRequirements;
using Bit.Core.SecretsManager.Models.Data;
using Bit.Core.SecretsManager.Queries.Interfaces;
using Bit.Core.SecretsManager.Repositories;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Commercial.Core.SecretsManager.AuthorizationHandlers.AccessPolicies;

public class ServiceAccountGrantedPoliciesAuthorizationHandler : AuthorizationHandler<
    ServiceAccountGrantedPoliciesOperationRequirement,
    ServiceAccountGrantedPoliciesUpdates>
{
    private readonly IAccessClientQuery _accessClientQuery;
    private readonly ICurrentContext _currentContext;
    private readonly IProjectRepository _projectRepository;
    private readonly IServiceAccountRepository _serviceAccountRepository;

    public ServiceAccountGrantedPoliciesAuthorizationHandler(ICurrentContext currentContext,
        IAccessClientQuery accessClientQuery,
        IProjectRepository projectRepository,
        IServiceAccountRepository serviceAccountRepository)
    {
        _currentContext = currentContext;
        _accessClientQuery = accessClientQuery;
        _serviceAccountRepository = serviceAccountRepository;
        _projectRepository = projectRepository;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        ServiceAccountGrantedPoliciesOperationRequirement requirement,
        ServiceAccountGrantedPoliciesUpdates resource)
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
            case not null when requirement == ServiceAccountGrantedPoliciesOperations.Updates:
                await CanUpdateAsync(context, requirement, resource, accessClient,
                    userId);
                break;
            default:
                throw new ArgumentException("Unsupported operation requirement type provided.",
                    nameof(requirement));
        }
    }

    private async Task CanUpdateAsync(AuthorizationHandlerContext context,
        ServiceAccountGrantedPoliciesOperationRequirement requirement, ServiceAccountGrantedPoliciesUpdates resource,
        AccessClientType accessClient, Guid userId)
    {
        var access =
            await _serviceAccountRepository.AccessToServiceAccountAsync(resource.ServiceAccountId, userId,
                accessClient);
        if (access.Write)
        {
            var projectIdsToCheck = resource.ProjectGrantedPolicyUpdates.Select(update =>
                update.AccessPolicy.GrantedProjectId!.Value).ToList();

            var sameOrganization =
                await _projectRepository.ProjectsAreInOrganization(projectIdsToCheck, resource.OrganizationId);
            if (!sameOrganization)
            {
                return;
            }

            var projectsAccess =
                await _projectRepository.AccessToProjectsAsync(projectIdsToCheck, userId, accessClient);
            if (projectsAccess.Count == projectIdsToCheck.Count && projectsAccess.All(a => a.Value.Write))
            {
                context.Succeed(requirement);
            }
        }
    }
}
