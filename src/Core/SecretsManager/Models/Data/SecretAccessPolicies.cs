#nullable enable
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Enums.AccessPolicies;
using Bit.Core.SecretsManager.Models.Data.AccessPolicyUpdates;

namespace Bit.Core.SecretsManager.Models.Data;

public class SecretAccessPolicies
{
    public SecretAccessPolicies(Guid secretId, Guid organizationId, List<BaseAccessPolicy> policies)
    {
        SecretId = secretId;
        OrganizationId = organizationId;

        UserAccessPolicies = policies
            .OfType<UserSecretAccessPolicy>()
            .ToList();

        GroupAccessPolicies = policies
            .OfType<GroupSecretAccessPolicy>()
            .ToList();

        ServiceAccountAccessPolicies = policies
            .OfType<ServiceAccountSecretAccessPolicy>()
            .ToList();
    }

    public SecretAccessPolicies()
    {
    }

    public Guid SecretId { get; set; }
    public Guid OrganizationId { get; set; }
    public IEnumerable<UserSecretAccessPolicy> UserAccessPolicies { get; set; } = [];
    public IEnumerable<GroupSecretAccessPolicy> GroupAccessPolicies { get; set; } = [];
    public IEnumerable<ServiceAccountSecretAccessPolicy> ServiceAccountAccessPolicies { get; set; } = [];

    public SecretAccessPoliciesUpdates GetPolicyUpdates(SecretAccessPolicies requested) =>
        new()
        {
            SecretId = SecretId,
            OrganizationId = OrganizationId,
            UserAccessPolicyUpdates = GetUserPolicyUpdates(requested.UserAccessPolicies.ToList()),
            GroupAccessPolicyUpdates = GetGroupPolicyUpdates(requested.GroupAccessPolicies.ToList()),
            ServiceAccountAccessPolicyUpdates =
                GetServiceAccountPolicyUpdates(requested.ServiceAccountAccessPolicies.ToList())
        };

    private static List<TPolicyUpdate> GetPolicyUpdates<TPolicy, TPolicyUpdate>(
        List<TPolicy> currentPolicies,
        List<TPolicy> requestedPolicies,
        Func<IEnumerable<TPolicy>, List<Guid>> getIds,
        Func<IEnumerable<TPolicy>, List<Guid>> getIdsToBeUpdated,
        Func<IEnumerable<TPolicy>, List<Guid>, AccessPolicyOperation, List<TPolicyUpdate>> createPolicyUpdates)
        where TPolicy : class
        where TPolicyUpdate : class
    {
        var currentIds = getIds(currentPolicies);
        var requestedIds = getIds(requestedPolicies);

        var idsToBeDeleted = currentIds.Except(requestedIds).ToList();
        var idsToBeCreated = requestedIds.Except(currentIds).ToList();
        var idsToBeUpdated = getIdsToBeUpdated(requestedPolicies);

        var policiesToBeDeleted = createPolicyUpdates(currentPolicies, idsToBeDeleted, AccessPolicyOperation.Delete);
        var policiesToBeCreated = createPolicyUpdates(requestedPolicies, idsToBeCreated, AccessPolicyOperation.Create);
        var policiesToBeUpdated = createPolicyUpdates(requestedPolicies, idsToBeUpdated, AccessPolicyOperation.Update);

        return policiesToBeDeleted.Concat(policiesToBeCreated).Concat(policiesToBeUpdated).ToList();
    }

    private static List<Guid> GetOrganizationUserIds(IEnumerable<UserSecretAccessPolicy> policies) =>
        policies.Select(ap => ap.OrganizationUserId!.Value).ToList();

    private static List<Guid> GetGroupIds(IEnumerable<GroupSecretAccessPolicy> policies) =>
        policies.Select(ap => ap.GroupId!.Value).ToList();

    private static List<Guid> GetServiceAccountIds(IEnumerable<ServiceAccountSecretAccessPolicy> policies) =>
        policies.Select(ap => ap.ServiceAccountId!.Value).ToList();

    private static List<UserSecretAccessPolicyUpdate> CreateUserPolicyUpdates(
        IEnumerable<UserSecretAccessPolicy> policies, List<Guid> userIds,
        AccessPolicyOperation operation) =>
        policies
            .Where(ap => userIds.Contains(ap.OrganizationUserId!.Value))
            .Select(ap => new UserSecretAccessPolicyUpdate { Operation = operation, AccessPolicy = ap })
            .ToList();

    private static List<GroupSecretAccessPolicyUpdate> CreateGroupPolicyUpdates(
        IEnumerable<GroupSecretAccessPolicy> policies, List<Guid> groupIds,
        AccessPolicyOperation operation) =>
        policies
            .Where(ap => groupIds.Contains(ap.GroupId!.Value))
            .Select(ap => new GroupSecretAccessPolicyUpdate { Operation = operation, AccessPolicy = ap })
            .ToList();

    private static List<ServiceAccountSecretAccessPolicyUpdate> CreateServiceAccountPolicyUpdates(
        IEnumerable<ServiceAccountSecretAccessPolicy> policies, List<Guid> serviceAccountIds,
        AccessPolicyOperation operation) =>
        policies
            .Where(ap => serviceAccountIds.Contains(ap.ServiceAccountId!.Value))
            .Select(ap => new ServiceAccountSecretAccessPolicyUpdate { Operation = operation, AccessPolicy = ap })
            .ToList();


    private List<UserSecretAccessPolicyUpdate> GetUserPolicyUpdates(List<UserSecretAccessPolicy> requestedPolicies) =>
        GetPolicyUpdates(UserAccessPolicies.ToList(), requestedPolicies, GetOrganizationUserIds, GetUserIdsToBeUpdated,
            CreateUserPolicyUpdates);

    private List<GroupSecretAccessPolicyUpdate>
        GetGroupPolicyUpdates(List<GroupSecretAccessPolicy> requestedPolicies) =>
        GetPolicyUpdates(GroupAccessPolicies.ToList(), requestedPolicies, GetGroupIds, GetGroupIdsToBeUpdated,
            CreateGroupPolicyUpdates);

    private List<ServiceAccountSecretAccessPolicyUpdate> GetServiceAccountPolicyUpdates(
        List<ServiceAccountSecretAccessPolicy> requestedPolicies) =>
        GetPolicyUpdates(ServiceAccountAccessPolicies.ToList(), requestedPolicies, GetServiceAccountIds,
            GetServiceAccountIdsToBeUpdated, CreateServiceAccountPolicyUpdates);

    private List<Guid> GetUserIdsToBeUpdated(IEnumerable<UserSecretAccessPolicy> requested) =>
        UserAccessPolicies
            .Where(currentAp => requested.Any(requestedAp =>
                requestedAp.GrantedSecretId == currentAp.GrantedSecretId &&
                requestedAp.OrganizationUserId == currentAp.OrganizationUserId &&
                (requestedAp.Write != currentAp.Write || requestedAp.Read != currentAp.Read)))
            .Select(ap => ap.OrganizationUserId!.Value)
            .ToList();

    private List<Guid> GetGroupIdsToBeUpdated(IEnumerable<GroupSecretAccessPolicy> requested) =>
        GroupAccessPolicies
            .Where(currentAp => requested.Any(requestedAp =>
                requestedAp.GrantedSecretId == currentAp.GrantedSecretId &&
                requestedAp.GroupId == currentAp.GroupId &&
                (requestedAp.Write != currentAp.Write || requestedAp.Read != currentAp.Read)))
            .Select(ap => ap.GroupId!.Value)
            .ToList();

    private List<Guid> GetServiceAccountIdsToBeUpdated(IEnumerable<ServiceAccountSecretAccessPolicy> requested) =>
        ServiceAccountAccessPolicies
            .Where(currentAp => requested.Any(requestedAp =>
                requestedAp.GrantedSecretId == currentAp.GrantedSecretId &&
                requestedAp.ServiceAccountId == currentAp.ServiceAccountId &&
                (requestedAp.Write != currentAp.Write || requestedAp.Read != currentAp.Read)))
            .Select(ap => ap.ServiceAccountId!.Value)
            .ToList();
}
