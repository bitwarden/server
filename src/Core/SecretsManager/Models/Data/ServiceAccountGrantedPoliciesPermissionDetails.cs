#nullable enable
using Bit.Core.SecretsManager.Entities;

namespace Bit.Core.SecretsManager.Models.Data;

public class ServiceAccountGrantedPoliciesPermissionDetails
{
    public Guid ServiceAccountId { get; set; }
    public Guid OrganizationId { get; set; }
    public required IEnumerable<ServiceAccountProjectAccessPolicyPermissionDetails> ProjectGrantedPolicies { get; set; }
}

public class ServiceAccountProjectAccessPolicyPermissionDetails
{
    public required ServiceAccountProjectAccessPolicy AccessPolicy { get; set; }
    public bool HasPermission { get; set; }
}
