using System.ComponentModel.DataAnnotations;
using Bit.Core.AdminConsole.Entities;
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
        if (string.IsNullOrWhiteSpace(existingSignup.PublicKey))
        {
            existingSignup.PublicKey = PublicKey;
        }

        if (string.IsNullOrWhiteSpace(existingSignup.PrivateKey))
        {
            existingSignup.PrivateKey = EncryptedPrivateKey;
        }

        return existingSignup;
    }

    public OrganizationUpgrade ToOrganizationUpgrade(OrganizationUpgrade existingUpgrade)
    {
        if (string.IsNullOrWhiteSpace(existingUpgrade.PublicKey))
        {
            existingUpgrade.PublicKey = PublicKey;
        }

        if (string.IsNullOrWhiteSpace(existingUpgrade.PrivateKey))
        {
            existingUpgrade.PrivateKey = EncryptedPrivateKey;
        }

        return existingUpgrade;
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
