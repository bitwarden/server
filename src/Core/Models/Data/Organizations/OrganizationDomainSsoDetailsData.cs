namespace Bit.Core.Models.Data.Organizations;

public class OrganizationDomainSsoDetailsData
{
    public Guid OrganizationId { get; set; }
    public string OrganizationName { get; set; }
    public string DomainName { get; set; }
    public bool SsoAvailable { get; set; }
    public string OrganizationIdentifier { get; set; }
    public DateTime? VerifiedDate { get; set; }
    public bool OrganizationEnabled { get; set; }
}
