namespace Bit.Core.Models.Data.Organizations.OrganizationUsers;

public class OrganizationSeatCounts
{
    public int Users { get; set; }
    public int Sponsored { get; set; }
    public int Total => Users + Sponsored;
}
