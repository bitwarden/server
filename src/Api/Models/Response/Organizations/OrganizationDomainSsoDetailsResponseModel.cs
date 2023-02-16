using Bit.Core.Models.Api;
using Bit.Core.Models.Data.Organizations;

namespace Bit.Api.Models.Response.Organizations;

public class OrganizationDomainSsoDetailsResponseModel : ResponseModel
{
    public OrganizationDomainSsoDetailsResponseModel(OrganizationDomainSsoDetailsData data, string obj = "organizationDomainSsoDetails")
        : base(obj)
    {
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        SsoAvailable = data.SsoAvailable;
        DomainName = data.DomainName;
        OrganizationIdentifier = data.OrganizationIdentifier;
        SsoRequired = data.SsoRequired;
        VerifiedDate = data.VerifiedDate;
    }

    public bool SsoAvailable { get; private set; }
    public string DomainName { get; private set; }
    public string OrganizationIdentifier { get; private set; }
    public bool SsoRequired { get; private set; }
    public DateTime? VerifiedDate { get; private set; }
}
