using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Commands.AccessPolicies.Interfaces;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;

namespace Bit.Commercial.Core.SecretsManager.Commands.AccessPolicies;

public class CreateAccessPoliciesCommand : ICreateAccessPoliciesCommand
{
    private readonly IAccessPolicyRepository _accessPolicyRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IServiceAccountRepository _serviceAccountRepository;

    public CreateAccessPoliciesCommand(
        IAccessPolicyRepository accessPolicyRepository,
        IProjectRepository projectRepository,
        IServiceAccountRepository serviceAccountRepository)
    {
        _accessPolicyRepository = accessPolicyRepository;
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

    private static void CheckForDistinctAccessPolicies(IReadOnlyCollection<BaseAccessPolicy> accessPolicies)
    {
        var distinctAccessPolicies = accessPolicies.DistinctBy(baseAccessPolicy =>
        {
            return baseAccessPolicy switch
            {
                UserProjectAccessPolicy ap => new Tuple<Guid?, Guid?>(ap.OrganizationUserId, ap.GrantedProjectId),
                GroupProjectAccessPolicy ap => new Tuple<Guid?, Guid?>(ap.GroupId, ap.GrantedProjectId),
                ServiceAccountProjectAccessPolicy ap => new Tuple<Guid?, Guid?>(ap.ServiceAccountId,
                    ap.GrantedProjectId),
                UserServiceAccountAccessPolicy ap => new Tuple<Guid?, Guid?>(ap.OrganizationUserId,
                    ap.GrantedServiceAccountId),
                GroupServiceAccountAccessPolicy ap => new Tuple<Guid?, Guid?>(ap.GroupId, ap.GrantedServiceAccountId),
                _ => throw new ArgumentException("Unsupported access policy type provided.", nameof(baseAccessPolicy)),
            };
        }).ToList();

        if (accessPolicies.Count != distinctAccessPolicies.Count)
        {
            throw new BadRequestException("Resources must be unique");
        }
    }

    public async Task<IEnumerable<BaseAccessPolicy>> CreateManyAsync(List<BaseAccessPolicy> accessPolicies, Guid userId, AccessClientType accessType)
    {
        CheckForDistinctAccessPolicies(accessPolicies);
        await CheckAccessPoliciesDoNotExistAsync(accessPolicies);
        await CheckCanCreateAsync(accessPolicies, userId, accessType);
        return await _accessPolicyRepository.CreateManyAsync(accessPolicies);
    }

    private async Task CheckCanCreateAsync(List<BaseAccessPolicy> accessPolicies, Guid userId, AccessClientType accessType)
    {
        var projectIds = GetDistinctGrantedProjectIds(accessPolicies).ToList();
        var serviceAccountIds = GetDistinctGrantedServiceAccountIds(accessPolicies).ToList();

        if (projectIds.Any())
        {
            foreach (var projectId in projectIds)
            {
                await CheckPermissionAsync(accessType, userId, projectId);
            }
        }
        if (serviceAccountIds.Any())
        {
            foreach (var serviceAccountId in serviceAccountIds)
            {
                await CheckPermissionAsync(accessType, userId, serviceAccountIdToCheck: serviceAccountId);
            }
        }

        if (!projectIds.Any() && !serviceAccountIds.Any())
        {
            throw new BadRequestException("No granted IDs specified");
        }
    }

    private async Task CheckAccessPoliciesDoNotExistAsync(List<BaseAccessPolicy> accessPolicies)
    {
        foreach (var accessPolicy in accessPolicies)
        {
            if (await _accessPolicyRepository.AccessPolicyExists(accessPolicy))
            {
                throw new BadRequestException("Resource already exists");
            }
        }
    }

    private async Task CheckPermissionAsync(AccessClientType accessClient, Guid userId, Guid? projectIdToCheck = null,
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
                    hasAccess = (await _projectRepository.AccessToProjectAsync(projectIdToCheck.Value, userId, accessClient)).Write;
                }
                else if (serviceAccountIdToCheck.HasValue)
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
            throw new NotFoundException();
        }
    }
}
