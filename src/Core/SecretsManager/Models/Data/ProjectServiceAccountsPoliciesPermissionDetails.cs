#nullable enable
namespace Bit.Core.SecretsManager.Models.Data;

public class ProjectServiceAccountsPoliciesPermissionDetails
{
    public Guid ProjectId { get; set; }
    public Guid OrganizationId { get; set; }
    public required IEnumerable<ServiceAccountProjectAccessPolicyPermissionDetails> ServiceAccountPoliciesDetails { get; set; }
}
