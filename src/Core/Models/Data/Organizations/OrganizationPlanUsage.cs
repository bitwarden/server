using Bit.Core.Entities;

namespace Bit.Core.Models.Data.Organizations;

public class OrganizationPlanUsage
{
    public IEnumerable<OrganizationUser> OrganizationUsers { get; set; }
    public int CollectionCount { get; set; }
    public int GroupCount { get; set; }
    public IEnumerable<Policy> Policies { get; set; }
    public SsoConfig SsoConfig { get; set; }
    public IEnumerable<OrganizationConnection> ScimConnections { get; set; }
}
