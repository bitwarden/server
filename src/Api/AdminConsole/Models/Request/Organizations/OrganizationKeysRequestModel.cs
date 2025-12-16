// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations;
using Bit.Core.Models.Business;

namespace Bit.Api.AdminConsole.Models.Request.Organizations;

public class OrganizationKeysRequestModel
{
    [Required]
    public string PublicKey { get; set; }
    [Required]
    public string EncryptedPrivateKey { get; set; }

    public OrganizationSignup ToOrganizationSignup(OrganizationSignup existingSignup)
    {
        if (existingSignup.Keys == null || (string.IsNullOrWhiteSpace(existingSignup.Keys.PublicKey) && string.IsNullOrWhiteSpace(existingSignup.Keys.PrivateKey)))
        {
            existingSignup.Keys = new OrganizationKeyPair
            {
                PublicKey = PublicKey,
                PrivateKey = EncryptedPrivateKey
            };
        }

        return existingSignup;
    }

    public Organization ToOrganization(Organization existingOrg)
    {
        if (string.IsNullOrWhiteSpace(existingOrg.PublicKey))
        {
            existingOrg.PublicKey = PublicKey;
        }

        if (string.IsNullOrWhiteSpace(existingOrg.PrivateKey))
        {
            existingOrg.PrivateKey = EncryptedPrivateKey;
        }

        return existingOrg;
    }
}
