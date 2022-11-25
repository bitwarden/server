using Bit.Core.Entities;
using Bit.Core.Models.Api;

namespace Bit.Api.Models.Response.Organizations;

public class OrganizationDomainResponseModel : ResponseModel
{
    public OrganizationDomainResponseModel(OrganizationDomain organizationDomain, string obj = "organizationDomain")
        : base(obj)
    {
        if (organizationDomain == null)
        {
            throw new ArgumentNullException(nameof(organizationDomain));
        }

        Id = organizationDomain.Id.ToString();
        OrganizationId = organizationDomain.OrganizationId.ToString();
        Txt = organizationDomain.Txt;
        DomainName = organizationDomain.DomainName;
        CreationDate = organizationDomain.CreationDate;
        NextRunDate = organizationDomain.NextRunDate;
        NextRunCount = organizationDomain.NextRunCount;
        VerifiedDate = organizationDomain.VerifiedDate;
    }

    public string Id { get; set; }
    public string OrganizationId { get; set; }
    public string Txt { get; set; }
    public string DomainName { get; set; }
    public DateTime CreationDate { get; set; }
    public DateTime NextRunDate { get; set; }
    public int NextRunCount { get; set; }
    public DateTime? VerifiedDate { get; set; }
}
