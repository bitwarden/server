#nullable enable

using Bit.Core.AdminConsole.Enums;

namespace Bit.Core.AdminConsole.Models.Data.Organizations.Policies;

public class OrganizationPolicyDetails
{
    public Guid OrganizationId { get; set; }
    public PolicyType PolicyType { get; set; }
    public string? PolicyData { get; set; }
    public IEnumerable<UserPolicyDetails> Users { get; set; } = [];
}
