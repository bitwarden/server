#nullable enable
using Bit.Core.SecretsManager.Entities;

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
}
