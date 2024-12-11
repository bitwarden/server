namespace Bit.Core.Models.Data.Organizations;

public class VerifiedOrganizationDomainSsoDetail
{
    public VerifiedOrganizationDomainSsoDetail() { }

    public VerifiedOrganizationDomainSsoDetail(
        Guid organizationId,
        string organizationName,
        string domainName,
        string organizationIdentifier
    )
    {
        OrganizationId = organizationId;
        OrganizationName = organizationName;
        DomainName = domainName;
        OrganizationIdentifier = organizationIdentifier;
    }

    public Guid OrganizationId { get; init; }
    public string OrganizationName { get; init; }
    public string DomainName { get; init; }
    public string OrganizationIdentifier { get; init; }
}
