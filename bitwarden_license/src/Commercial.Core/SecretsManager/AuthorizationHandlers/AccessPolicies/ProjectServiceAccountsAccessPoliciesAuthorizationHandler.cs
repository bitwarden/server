#nullable enable
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.SecretsManager.AuthorizationRequirements;
using Bit.Core.SecretsManager.Enums.AccessPolicies;
using Bit.Core.SecretsManager.Models.Data.AccessPolicyUpdates;
using Bit.Core.SecretsManager.Queries.Interfaces;
using Bit.Core.SecretsManager.Repositories;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Commercial.Core.SecretsManager.AuthorizationHandlers.AccessPolicies;

public class ProjectServiceAccountsAccessPoliciesAuthorizationHandler : AuthorizationHandler<
    ProjectServiceAccountsAccessPoliciesOperationRequirement,
    ProjectServiceAccountsAccessPoliciesUpdates>
{
    private readonly IAccessClientQuery _accessClientQuery;
    private readonly ICurrentContext _currentContext;
    private readonly IProjectRepository _projectRepository;
    private readonly IServiceAccountRepository _serviceAccountRepository;

    public ProjectServiceAccountsAccessPoliciesAuthorizationHandler(ICurrentContext currentContext,
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
        ProjectServiceAccountsAccessPoliciesOperationRequirement requirement,
        ProjectServiceAccountsAccessPoliciesUpdates resource)
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
            case not null when requirement == ProjectServiceAccountsAccessPoliciesOperations.Updates:
                await CanUpdateAsync(context, requirement, resource, accessClient,
                    userId);
                break;
            default:
                throw new ArgumentException("Unsupported operation requirement type provided.",
                    nameof(requirement));
        }
    }

    private async Task CanUpdateAsync(AuthorizationHandlerContext context,
        ProjectServiceAccountsAccessPoliciesOperationRequirement requirement,
        ProjectServiceAccountsAccessPoliciesUpdates resource,
        AccessClientType accessClient, Guid userId)
    {
        var access =
            await _projectRepository.AccessToProjectAsync(resource.ProjectId, userId,
                accessClient);
        if (!access.Write)
        {
            return;
        }

        var serviceAccountIds = resource.ServiceAccountAccessPolicyUpdates.Select(update =>
            update.AccessPolicy.ServiceAccountId!.Value).ToList();

        var inSameOrganization =
            await _serviceAccountRepository.ServiceAccountsAreInOrganizationAsync(serviceAccountIds,
                resource.OrganizationId);
        if (!inSameOrganization)
        {
            return;
        }

        // Users can only create access policies for service accounts they have access to.
        // User can delete and update any service account access policy if they have write access to the project.
        var serviceAccountIdsToCheck = resource.ServiceAccountAccessPolicyUpdates
            .Where(update => update.Operation == AccessPolicyOperation.Create).Select(update =>
                update.AccessPolicy.ServiceAccountId!.Value).ToList();

        if (serviceAccountIdsToCheck.Count == 0)
        {
            context.Succeed(requirement);
            return;
        }

        var serviceAccountsAccess =
            await _serviceAccountRepository.AccessToServiceAccountsAsync(serviceAccountIdsToCheck, userId,
                accessClient);
        if (serviceAccountsAccess.Count == serviceAccountIdsToCheck.Count &&
            serviceAccountsAccess.All(a => a.Value.Write))
        {
            context.Succeed(requirement);
        }
    }
}
