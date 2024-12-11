using Bit.Core.Models.Api;
using Bit.Core.Models.Data.Organizations;

namespace Bit.Api.AdminConsole.Models.Response.Organizations;

public class VerifiedOrganizationDomainSsoDetailResponseModel : ResponseModel
{
    public VerifiedOrganizationDomainSsoDetailResponseModel(
        VerifiedOrganizationDomainSsoDetail data
    )
        : base("verifiedOrganizationDomainSsoDetails")
    {
        ArgumentNullException.ThrowIfNull(data);

        DomainName = data.DomainName;
        OrganizationIdentifier = data.OrganizationIdentifier;
        OrganizationName = data.OrganizationName;
    }

    public string DomainName { get; }
    public string OrganizationIdentifier { get; }
    public string OrganizationName { get; }
}
