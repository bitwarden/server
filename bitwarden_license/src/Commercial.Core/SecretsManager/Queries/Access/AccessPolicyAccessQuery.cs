using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Queries.Access.Interfaces;
using Bit.Core.SecretsManager.Repositories;

namespace Bit.Commercial.Core.SecretsManager.Queries.Access;

public class AccessPolicyAccessQuery : IAccessPolicyAccessQuery
{
    private readonly ICurrentContext _currentContext;
    private readonly IProjectRepository _projectRepository;
    private readonly IServiceAccountRepository _serviceAccountRepository;

    public AccessPolicyAccessQuery(ICurrentContext currentContext, IProjectRepository projectRepository, IServiceAccountRepository serviceAccountRepository)
    {
        _currentContext = currentContext;
        _projectRepository = projectRepository;
        _serviceAccountRepository = serviceAccountRepository;
    }

    private static IEnumerable<Guid?> GetDistinctGrantedProjectIds(List<BaseAccessPolicy> accessPolicies)
    {
        var userGrantedIds = accessPolicies.OfType<UserProjectAccessPolicy>().Select(ap => ap.GrantedProjectId);
        var groupGrantedIds = accessPolicies.OfType<GroupProjectAccessPolicy>().Select(ap => ap.GrantedProjectId);
        var saGrantedIds = accessPolicies.OfType<ServiceAccountProjectAccessPolicy>().Select(ap => ap.GrantedProjectId);
        return userGrantedIds.Concat(groupGrantedIds).Concat(saGrantedIds).Distinct();
    }

    private static IEnumerable<Guid?> GetDistinctGrantedServiceAccountIds(List<BaseAccessPolicy> accessPolicies)
    {
        var userGrantedIds = accessPolicies.OfType<UserServiceAccountAccessPolicy>().Select(ap => ap.GrantedServiceAccountId);
        var groupGrantedIds = accessPolicies.OfType<GroupServiceAccountAccessPolicy>()
            .Select(ap => ap.GrantedServiceAccountId);
        return userGrantedIds.Concat(groupGrantedIds).Distinct();
    }

    public async Task<bool> HasAccess(BaseAccessPolicy baseAccessPolicy, Guid userId)
    {
        return baseAccessPolicy switch
        {
            UserProjectAccessPolicy ap => await HasPermissionsAsync(ap.GrantedProject!.OrganizationId, userId,
                ap.GrantedProjectId),
            GroupProjectAccessPolicy ap => await HasPermissionsAsync(ap.GrantedProject!.OrganizationId, userId,
                ap.GrantedProjectId),
            ServiceAccountProjectAccessPolicy ap => await HasPermissionsAsync(ap.GrantedProject!.OrganizationId, userId,
                ap.GrantedProjectId),
            UserServiceAccountAccessPolicy ap => await HasPermissionsAsync(ap.GrantedServiceAccount!.OrganizationId,
                userId, serviceAccountIdToCheck: ap.GrantedServiceAccountId),
            GroupServiceAccountAccessPolicy ap => await HasPermissionsAsync(ap.GrantedServiceAccount!.OrganizationId,
                userId, serviceAccountIdToCheck: ap.GrantedServiceAccountId),
            _ => throw new ArgumentException("Unsupported access policy type provided.")
        };
    }

    public async Task<bool> HasAccess(List<BaseAccessPolicy> accessPolicies, Guid organizationId, Guid userId)
    {
        if (!_currentContext.AccessSecretsManager(organizationId))
        {
            return false;
        }

        var orgAdmin = await _currentContext.OrganizationAdmin(organizationId);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.ClientType, orgAdmin);

        var projectIds = GetDistinctGrantedProjectIds(accessPolicies).ToList();
        var serviceAccountIds = GetDistinctGrantedServiceAccountIds(accessPolicies).ToList();

        if (!projectIds.Any() && !serviceAccountIds.Any())
        {
            return false;
        }

        if (projectIds.Any())
        {
            foreach (var projectId in projectIds)
            {
                if (!await HasPermissionToIdAsync(accessClient, userId, projectId))
                {
                    return false;
                }
            }
        }

        if (serviceAccountIds.Any())
        {
            foreach (var serviceAccountId in serviceAccountIds)
            {
                if (!await HasPermissionToIdAsync(accessClient, userId, serviceAccountIdToCheck: serviceAccountId))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private async Task<bool> HasPermissionsAsync(
        Guid organizationId,
        Guid userId,
        Guid? projectIdToCheck = null,
        Guid? serviceAccountIdToCheck = null)
    {
        if (!_currentContext.AccessSecretsManager(organizationId))
        {
            return false;
        }

        var orgAdmin = await _currentContext.OrganizationAdmin(organizationId);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.ClientType, orgAdmin);
        return await HasPermissionToIdAsync(accessClient, userId, projectIdToCheck, serviceAccountIdToCheck);
    }

    private async Task<bool> HasPermissionToIdAsync(AccessClientType accessClient, Guid userId,
        Guid? projectIdToCheck = null,
        Guid? serviceAccountIdToCheck = null)
    {
        bool hasAccess;
        switch (accessClient)
        {
            case AccessClientType.NoAccessCheck:
                hasAccess = true;
                break;
            case AccessClientType.User:
                if (projectIdToCheck.HasValue)
                {
                    hasAccess = await _projectRepository.UserHasWriteAccessToProject(projectIdToCheck.Value, userId);
                }
                else if (serviceAccountIdToCheck.HasValue)
                {
                    hasAccess = (await _serviceAccountRepository.AccessToServiceAccountAsync(
                        serviceAccountIdToCheck.Value, userId, accessClient)).Write;
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

        return hasAccess;
    }
}
