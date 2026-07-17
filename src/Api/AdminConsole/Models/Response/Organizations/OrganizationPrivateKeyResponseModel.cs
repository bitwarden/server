// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.Models.Api;

namespace Bit.Api.AdminConsole.Models.Response.Organizations;

public class OrganizationPrivateKeyResponseModel : ResponseModel
{
    public OrganizationPrivateKeyResponseModel(Organization org) : base("organizationPrivateKey")
    {
        if (org == null)
        {
            throw new ArgumentNullException(nameof(org));
        }

        PrivateKey = org.PrivateKey;
    }

    public string PrivateKey { get; set; }
}
