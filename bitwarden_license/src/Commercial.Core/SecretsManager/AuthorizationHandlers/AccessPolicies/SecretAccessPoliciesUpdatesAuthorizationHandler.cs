#nullable enable
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.SecretsManager.AuthorizationRequirements;
using Bit.Core.SecretsManager.Enums.AccessPolicies;
using Bit.Core.SecretsManager.Models.Data.AccessPolicyUpdates;
using Bit.Core.SecretsManager.Queries.AccessPolicies.Interfaces;
using Bit.Core.SecretsManager.Queries.Interfaces;
using Bit.Core.SecretsManager.Repositories;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Commercial.Core.SecretsManager.AuthorizationHandlers.AccessPolicies;

public class SecretAccessPoliciesUpdatesAuthorizationHandler : AuthorizationHandler<
    SecretAccessPoliciesOperationRequirement,
    SecretAccessPoliciesUpdates>
{
    private readonly IAccessClientQuery _accessClientQuery;
    private readonly ICurrentContext _currentContext;
    private readonly ISameOrganizationQuery _sameOrganizationQuery;
    private readonly ISecretRepository _secretRepository;
    private readonly IServiceAccountRepository _serviceAccountRepository;

    public SecretAccessPoliciesUpdatesAuthorizationHandler(ICurrentContext currentContext,
        IAccessClientQuery accessClientQuery,
        ISecretRepository secretRepository,
        ISameOrganizationQuery sameOrganizationQuery,
        IServiceAccountRepository serviceAccountRepository)
    {
        _currentContext = currentContext;
        _accessClientQuery = accessClientQuery;
        _sameOrganizationQuery = sameOrganizationQuery;
        _serviceAccountRepository = serviceAccountRepository;
        _secretRepository = secretRepository;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        SecretAccessPoliciesOperationRequirement requirement,
        SecretAccessPoliciesUpdates resource)
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
            case not null when requirement == SecretAccessPoliciesOperations.Updates:
                await CanUpdateAsync(context, requirement, resource, accessClient,
                    userId);
                break;
            case not null when requirement == SecretAccessPoliciesOperations.Create:
                await CanCreateAsync(context, requirement, resource, accessClient,
                    userId);
                break;
            default:
                throw new ArgumentException("Unsupported operation requirement type provided.",
                    nameof(requirement));
        }
    }

    private async Task CanUpdateAsync(AuthorizationHandlerContext context,
        SecretAccessPoliciesOperationRequirement requirement,
        SecretAccessPoliciesUpdates resource,
        AccessClientType accessClient, Guid userId)
    {
        var access = await _secretRepository
            .AccessToSecretAsync(resource.SecretId, userId, accessClient);
        if (!access.Write)
        {
            return;
        }

        if (!await GranteesInTheSameOrganizationAsync(resource))
        {
            return;
        }

        // Users can only create access policies for service accounts they have access to.
        // User can delete and update any service account access policy if they have write access to the secret.
        if (await HasAccessToTargetServiceAccountsAsync(resource, accessClient, userId))
        {
            context.Succeed(requirement);
        }
    }

    private async Task CanCreateAsync(AuthorizationHandlerContext context,
        SecretAccessPoliciesOperationRequirement requirement,
        SecretAccessPoliciesUpdates resource,
        AccessClientType accessClient, Guid userId)
    {
        if (resource.UserAccessPolicyUpdates.Any(x => x.Operation != AccessPolicyOperation.Create) ||
            resource.GroupAccessPolicyUpdates.Any(x => x.Operation != AccessPolicyOperation.Create) ||
            resource.ServiceAccountAccessPolicyUpdates.Any(x => x.Operation != AccessPolicyOperation.Create))
        {
            return;
        }

        if (!await GranteesInTheSameOrganizationAsync(resource))
        {
            return;
        }

        // Users can only create access policies for service accounts they have access to.
        if (await HasAccessToTargetServiceAccountsAsync(resource, accessClient, userId))
        {
            context.Succeed(requirement);
        }
    }

    private async Task<bool> GranteesInTheSameOrganizationAsync(SecretAccessPoliciesUpdates resource)
    {
        var organizationUserIds = resource.UserAccessPolicyUpdates.Select(update =>
            update.AccessPolicy.OrganizationUserId!.Value).ToList();
        var groupIds = resource.GroupAccessPolicyUpdates.Select(update =>
            update.AccessPolicy.GroupId!.Value).ToList();
        var serviceAccountIds = resource.ServiceAccountAccessPolicyUpdates.Select(update =>
            update.AccessPolicy.ServiceAccountId!.Value).ToList();

        var usersInSameOrg = organizationUserIds.Count == 0 ||
                             await _sameOrganizationQuery.OrgUsersInTheSameOrgAsync(organizationUserIds,
                                 resource.OrganizationId);

        var groupsInSameOrg = groupIds.Count == 0 ||
                              await _sameOrganizationQuery.GroupsInTheSameOrgAsync(groupIds, resource.OrganizationId);

        var serviceAccountsInSameOrg = serviceAccountIds.Count == 0 ||
                                       await _serviceAccountRepository.ServiceAccountsAreInOrganizationAsync(
                                           serviceAccountIds,
                                           resource.OrganizationId);

        return usersInSameOrg && groupsInSameOrg && serviceAccountsInSameOrg;
    }

    private async Task<bool> HasAccessToTargetServiceAccountsAsync(SecretAccessPoliciesUpdates resource,
        AccessClientType accessClient, Guid userId)
    {
        var serviceAccountIdsToCheck = resource.ServiceAccountAccessPolicyUpdates
            .Where(update => update.Operation == AccessPolicyOperation.Create).Select(update =>
                update.AccessPolicy.ServiceAccountId!.Value).ToList();

        if (serviceAccountIdsToCheck.Count == 0)
        {
            return true;
        }

        var serviceAccountsAccess =
            await _serviceAccountRepository.AccessToServiceAccountsAsync(serviceAccountIdsToCheck, userId,
                accessClient);

        return serviceAccountsAccess.Count == serviceAccountIdsToCheck.Count &&
               serviceAccountsAccess.All(a => a.Value.Write);
    }
}
