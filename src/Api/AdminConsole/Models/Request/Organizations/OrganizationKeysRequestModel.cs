// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;
using Bit.Core.KeyManagement.Models.Data;

namespace Bit.Api.AdminConsole.Models.Request.Organizations;

public class OrganizationKeysRequestModel
{
    [Required]
    public string PublicKey { get; set; }
    [Required]
    public string EncryptedPrivateKey { get; set; }

    public PublicKeyEncryptionKeyPairData ToPublicKeyEncryptionKeyPairData()
    {
        return new PublicKeyEncryptionKeyPairData(
            wrappedPrivateKey: EncryptedPrivateKey,
            publicKey: PublicKey);
    }
}
