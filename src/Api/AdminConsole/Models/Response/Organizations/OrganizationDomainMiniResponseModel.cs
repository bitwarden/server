using Bit.Core.Entities;
using Bit.HttpExtensions;

namespace Bit.Api.AdminConsole.Models.Response.Organizations;

/// <summary>
/// A slim view of an organization's claimed domain, containing only the domain name and whether it has been
/// verified. It deliberately omits the DNS verification token and the domain verification job metadata exposed by
/// <see cref="OrganizationDomainResponseModel"/>, so it can be returned to members who administer the organization
/// without granting them visibility into its SSO configuration.
/// </summary>
public class OrganizationDomainMiniResponseModel : ResponseModel
{
    public OrganizationDomainMiniResponseModel(OrganizationDomain organizationDomain, string obj = "organizationDomainMini")
        : base(obj)
    {
        if (organizationDomain == null)
        {
            throw new ArgumentNullException(nameof(organizationDomain));
        }

        DomainName = organizationDomain.DomainName;
        VerifiedDate = organizationDomain.VerifiedDate;
    }

    public string DomainName { get; set; }
    public DateTime? VerifiedDate { get; set; }
}
