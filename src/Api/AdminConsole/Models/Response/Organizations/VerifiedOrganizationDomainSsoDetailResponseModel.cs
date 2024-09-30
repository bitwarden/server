using Bit.Core.Models.Api;
using Bit.Core.Models.Data.Organizations;

namespace Bit.Api.AdminConsole.Models.Response.Organizations;

public class VerifiedOrganizationDomainSsoDetailResponseModel : ResponseModel
{
    public VerifiedOrganizationDomainSsoDetailResponseModel(VerifiedOrganizationDomainSsoDetail data)
        : base("verifiedOrganizationDomainSsoDetails")
    {
        if (data is null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        DomainName = data.DomainName;
        OrganizationIdentifier = data.OrganizationIdentifier;
    }
    public string DomainName { get; }
    public string OrganizationIdentifier { get; }
}
