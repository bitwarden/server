﻿using Bit.Core.AdminConsole.Entities;
using Bit.Core.Models.Api;

namespace Bit.Api.AdminConsole.Models.Response.Organizations;

public class OrganizationKeysResponseModel : ResponseModel
{
    public OrganizationKeysResponseModel(Organization org)
        : base("organizationKeys")
    {
        ArgumentNullException.ThrowIfNull(org);

        PublicKey = org.PublicKey;
        PrivateKey = org.PrivateKey;
    }

    public string PublicKey { get; set; }
    public string PrivateKey { get; set; }
}
