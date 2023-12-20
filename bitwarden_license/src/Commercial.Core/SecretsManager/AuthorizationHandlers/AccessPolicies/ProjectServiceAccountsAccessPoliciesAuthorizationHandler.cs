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
    ProjectServiceAccountsAccessPoliciesAuthorizationHandler : AuthorizationHandler<
        ProjectServiceAccountsAccessPoliciesOperationRequirement,
        ProjectServiceAccountsAccessPolicies>
{
    private readonly IAccessClientQuery _accessClientQuery;
    private readonly ICurrentContext _currentContext;
    private readonly IProjectRepository _projectRepository;
    private readonly ISameOrganizationQuery _sameOrganizationQuery;
    private readonly IServiceAccountRepository _serviceAccountRepository;

    public ProjectServiceAccountsAccessPoliciesAuthorizationHandler(ICurrentContext currentContext,
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
        ProjectServiceAccountsAccessPoliciesOperationRequirement requirement,
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
            case not null when requirement == ProjectServiceAccountsAccessPoliciesOperations.Replace:
                await CanReplaceProjectServiceAccountsAsync(context, requirement, resource, accessClient, userId);
                break;
            default:
                throw new ArgumentException("Unsupported operation requirement type provided.",
                    nameof(requirement));
        }
    }

    private async Task CanReplaceProjectServiceAccountsAsync(AuthorizationHandlerContext context,
       ProjectServiceAccountsAccessPoliciesOperationRequirement requirement, ProjectServiceAccountsAccessPolicies resource,
       AccessClientType accessClient, Guid userId)
    {
        var projectAccess = await _projectRepository.AccessToProjectAsync(resource.Id, userId, accessClient);
        if (projectAccess.Write)
        {
            if (resource.ServiceAccountProjectsAccessPolicies != null && resource.ServiceAccountProjectsAccessPolicies.Any())
            {
                var serviceAccountIds = resource.ServiceAccountProjectsAccessPolicies.Select(ap => ap.ServiceAccountId!.Value).ToList();
                if (!await _sameOrganizationQuery.ServiceAccountsInTheSameOrgAsync(serviceAccountIds, resource.OrganizationId))
                {
                    return;
                }

                var serviceAccountAccess = await _serviceAccountRepository.AccessToServiceAccountsAsync(serviceAccountIds, userId, accessClient);
                if(!serviceAccountAccess.Write)
                {
                    return;
                }
            }

            context.Succeed(requirement);
        }
    }
}
