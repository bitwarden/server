#nullable enable
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Entities;

namespace Bit.Api.SecretsManager.Utilities;

public static class AccessPolicyHelpers
{
    public static void CheckForDistinctAccessPolicies(IReadOnlyCollection<BaseAccessPolicy> accessPolicies)
    {
        var distinctAccessPolicies = accessPolicies.DistinctBy(baseAccessPolicy =>
        {
            return baseAccessPolicy switch
            {
                UserProjectAccessPolicy ap => new Tuple<Guid?, Guid?>(ap.OrganizationUserId, ap.GrantedProjectId),
                UserSecretAccessPolicy ap => new Tuple<Guid?, Guid?>(ap.OrganizationUserId, ap.GrantedSecretId),
                UserServiceAccountAccessPolicy ap => new Tuple<Guid?, Guid?>(ap.OrganizationUserId,
                    ap.GrantedServiceAccountId),
                GroupProjectAccessPolicy ap => new Tuple<Guid?, Guid?>(ap.GroupId, ap.GrantedProjectId),
                GroupSecretAccessPolicy ap => new Tuple<Guid?, Guid?>(ap.GroupId, ap.GrantedSecretId),
                GroupServiceAccountAccessPolicy ap => new Tuple<Guid?, Guid?>(ap.GroupId, ap.GrantedServiceAccountId),
                ServiceAccountProjectAccessPolicy ap => new Tuple<Guid?, Guid?>(ap.ServiceAccountId,
                    ap.GrantedProjectId),
                ServiceAccountSecretAccessPolicy ap => new Tuple<Guid?, Guid?>(ap.ServiceAccountId,
                    ap.GrantedSecretId),
                _ => throw new ArgumentException("Unsupported access policy type provided.", nameof(baseAccessPolicy)),
            };
        }).ToList();

        if (accessPolicies.Count != distinctAccessPolicies.Count)
        {
            throw new BadRequestException("Resources must be unique");
        }
    }

    public static void CheckAccessPoliciesHaveReadPermission(IEnumerable<BaseAccessPolicy> accessPolicies)
    {
        var accessPoliciesPermission = accessPolicies.All(policy => policy.Read);
        if (!accessPoliciesPermission)
        {
            throw new BadRequestException("Resources must be Read = true");
        }
    }
}
