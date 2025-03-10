#nullable enable
using Bit.Core.SecretsManager.Enums.AccessPolicies;

namespace Bit.Core.SecretsManager.Models.Data.AccessPolicyUpdates;

public class SecretAccessPoliciesUpdates
{
    public SecretAccessPoliciesUpdates(SecretAccessPolicies accessPolicies)
    {
        SecretId = accessPolicies.SecretId;
        OrganizationId = accessPolicies.OrganizationId;
        UserAccessPolicyUpdates =
            accessPolicies.UserAccessPolicies.Select(x =>
                new UserSecretAccessPolicyUpdate { Operation = AccessPolicyOperation.Create, AccessPolicy = x });

        GroupAccessPolicyUpdates =
            accessPolicies.GroupAccessPolicies.Select(x =>
                new GroupSecretAccessPolicyUpdate { Operation = AccessPolicyOperation.Create, AccessPolicy = x });

        ServiceAccountAccessPolicyUpdates = accessPolicies.ServiceAccountAccessPolicies.Select(x =>
            new ServiceAccountSecretAccessPolicyUpdate { Operation = AccessPolicyOperation.Create, AccessPolicy = x });
    }

    public SecretAccessPoliciesUpdates() { }

    public Guid SecretId { get; set; }
    public Guid OrganizationId { get; set; }
    public IEnumerable<UserSecretAccessPolicyUpdate> UserAccessPolicyUpdates { get; set; } = [];
    public IEnumerable<GroupSecretAccessPolicyUpdate> GroupAccessPolicyUpdates { get; set; } = [];
    public IEnumerable<ServiceAccountSecretAccessPolicyUpdate> ServiceAccountAccessPolicyUpdates { get; set; } = [];

    public bool HasUpdates() =>
        UserAccessPolicyUpdates.Any() ||
        GroupAccessPolicyUpdates.Any() ||
        ServiceAccountAccessPolicyUpdates.Any();
}
