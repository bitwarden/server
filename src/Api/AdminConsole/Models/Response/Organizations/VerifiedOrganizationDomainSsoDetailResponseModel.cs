using Bit.Core.Models.Api;
using Bit.Core.Models.Data.Organizations;

namespace Bit.Api.AdminConsole.Models.Response.Organizations;

public class VerifiedOrganizationDomainSsoDetailResponseModel : ResponseModel
{
    public VerifiedOrganizationDomainSsoDetailResponseModel(VerifiedOrganizationDomainSsoDetailData data)
        : base("verifiedOrganizationDomainSsoDetails")
    {
        if (data is null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        SsoAvailable = data.SsoAvailable;
        DomainName = data.DomainName;
        OrganizationIdentifier = data.OrganizationIdentifier;
        VerifiedDate = data.VerifiedDate;
    }

    public bool SsoAvailable { get; }
    public string DomainName { get; }
    public string OrganizationIdentifier { get; }
    public DateTime VerifiedDate { get; }
}
