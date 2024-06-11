using Bit.Core.AdminConsole.Entities;
using Bit.Core.Models.Api;

namespace Bit.Api.AdminConsole.Models.Response.Organizations;

public class OrganizationPublicKeyResponseModel : ResponseModel
{
    public OrganizationPublicKeyResponseModel(Organization org) : base("organizationPublicKey")
    {
        if (org == null)
        {
            throw new ArgumentNullException(nameof(org));
        }

        PublicKey = org.PublicKey;
    }

    public string PublicKey { get; set; }
}
