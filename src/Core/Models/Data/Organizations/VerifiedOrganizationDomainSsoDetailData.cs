namespace Bit.Core.Models.Data.Organizations;

public class VerifiedOrganizationDomainSsoDetailData
{
    public VerifiedOrganizationDomainSsoDetailData()
    {
    }

    public VerifiedOrganizationDomainSsoDetailData(Guid organizationId, string organizationName, string domainName,
        bool ssoAvailable, string organizationIdentifier, DateTime verifiedDate)
    {
        OrganizationId = organizationId;
        OrganizationName = organizationName;
        DomainName = domainName;
        SsoAvailable = ssoAvailable;
        OrganizationIdentifier = organizationIdentifier;
        VerifiedDate = verifiedDate;
    }

    public Guid OrganizationId { get; init; }
    public string OrganizationName { get; init; }
    public string DomainName { get; init; }
    public bool SsoAvailable { get; init; }
    public string OrganizationIdentifier { get; init; }
    public DateTime VerifiedDate { get; init; }
}
