﻿using Bit.Core.Models.Api;
using Bit.Core.Models.Data.Organizations;

namespace Bit.Api.AdminConsole.Models.Response.Organizations;

public class OrganizationDomainSsoDetailsResponseModel : ResponseModel
{
    public OrganizationDomainSsoDetailsResponseModel(
        OrganizationDomainSsoDetailsData data,
        string obj = "organizationDomainSsoDetails"
    )
        : base(obj)
    {
        ArgumentNullException.ThrowIfNull(data);

        SsoAvailable = data.SsoAvailable;
        DomainName = data.DomainName;
        OrganizationIdentifier = data.OrganizationIdentifier;
        VerifiedDate = data.VerifiedDate;
    }

    public bool SsoAvailable { get; private set; }
    public string DomainName { get; private set; }
    public string OrganizationIdentifier { get; private set; }
    public DateTime? VerifiedDate { get; private set; }
}
