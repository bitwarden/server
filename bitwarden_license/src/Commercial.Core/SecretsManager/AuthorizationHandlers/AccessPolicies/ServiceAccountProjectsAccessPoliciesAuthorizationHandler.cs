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
    ServiceAccountProjectsAccessPoliciesAuthorizationHandler : AuthorizationHandler<
        ServiceAccountProjectsAccessPoliciesOperationRequirement,
        ProjectServiceAccountsAccessPolicies>
{
    private readonly IAccessClientQuery _accessClientQuery;
    private readonly ICurrentContext _currentContext;
    private readonly IProjectRepository _projectRepository;
    private readonly ISameOrganizationQuery _sameOrganizationQuery;
    private readonly IServiceAccountRepository _serviceAccountRepository;

    public ServiceAccountProjectsAccessPoliciesAuthorizationHandler(ICurrentContext currentContext,
        IAccessClientQuery accessClientQuery,
        ISameOrganizationQuery sameOrganizationQuery,
        IProjectRepository projectRepository,
        IServiceAccountRepository serviceAccountRepository)
    {
        _currentContext = currentContext;
        _accessClientQuery = accessClientQuery;
        _sameOrganizationQuery = sameOrganizationQuery;
        _projectRepository = projectRepository;
        _serviceAccountRepository = serviceAccountRepository;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        ServiceAccountProjectsAccessPoliciesOperationRequirement requirement,
        ProjectServiceAccountsAccessPolicies resource)
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
            case not null when requirement == ServiceAccountProjectsAccessPoliciesOperations.Replace:
                await CanReplaceServiceAccountProjectsAsync(context, requirement, resource, accessClient, userId);
                break;
 
            default:
                throw new ArgumentException("Unsupported operation requirement type provided.",
                    nameof(requirement));
        }
    }

    private async Task CanReplaceServiceAccountProjectsAsync(AuthorizationHandlerContext context,
       ServiceAccountProjectsAccessPoliciesOperationRequirement requirement, ProjectServiceAccountsAccessPolicies resource,
       AccessClientType accessClient, Guid userId)
    {
        var serviceAccountAccess = await _serviceAccountRepository.AccessToServiceAccountAsync(resource.Id, userId, accessClient);

        if (serviceAccountAccess.Write)
        {
            if (resource.ServiceAccountProjectsAccessPolicies != null && resource.ServiceAccountProjectsAccessPolicies.Any())
            {
                var projects = resource.ServiceAccountProjectsAccessPolicies.Select(ap => ap.GrantedProjectId!.Value).ToList();
                if (!await _sameOrganizationQuery.ProjectsInTheSameOrgAsync(projects, resource.OrganizationId))
                {
                    return;
                }

                var projectsAccess = await _projectRepository.AccessToProjectsAsync(projects, userId, accessClient);
                if (!projectsAccess.Write)
                {
                    return;
                }
            }

            context.Succeed(requirement);
        }
    }
}
