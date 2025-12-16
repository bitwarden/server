// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations;

namespace Bit.Api.Billing.Models.Requests;

// ReSharper disable once ClassNeverInstantiated.Global
public class KeyPairRequestBody
{
    [Required(ErrorMessage = "'publicKey' must be provided")]
    public string PublicKey { get; set; }
    [Required(ErrorMessage = "'encryptedPrivateKey' must be provided")]
    public string EncryptedPrivateKey { get; set; }

    public OrganizationKeyPair ToOrganizationKeyPair()
    {
        return new OrganizationKeyPair
        {
            PublicKey = PublicKey,
            PrivateKey = EncryptedPrivateKey
        };
    }
}
