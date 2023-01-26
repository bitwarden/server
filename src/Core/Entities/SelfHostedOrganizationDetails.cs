namespace Bit.Core.Entities;

public class SelfHostedOrganizationDetails : Organization
{
    public int OccupiedSeatCount { get; set; }
    public int CollectionCount { get; set; }
    public int GroupCount { get; set; }
    public IEnumerable<OrganizationUser> OrganizationUsers { get; set; }
    public IEnumerable<Policy> Policies { get; set; }
    public SsoConfig SsoConfig { get; set; }
    public IEnumerable<OrganizationConnection> ScimConnections { get; set; }
}
