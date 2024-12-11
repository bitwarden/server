#nullable enable
namespace Bit.Core.SecretsManager.Models.Data.AccessPolicyUpdates;

public class ProjectServiceAccountsAccessPoliciesUpdates
{
    public Guid ProjectId { get; set; }
    public Guid OrganizationId { get; set; }
    public IEnumerable<ServiceAccountProjectAccessPolicyUpdate> ServiceAccountAccessPolicyUpdates { get; set; } =
        [];
}
