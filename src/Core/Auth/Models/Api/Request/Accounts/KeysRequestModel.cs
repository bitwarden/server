using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;

namespace Bit.Core.Auth.Models.Api.Request.Accounts;

public class KeysRequestModel
{
    public string PublicKey { get; set; }
    [Required]
    public string EncryptedPrivateKey { get; set; }

    public User ToUser(User existingUser)
    {
        if (string.IsNullOrWhiteSpace(existingUser.PublicKey) && string.IsNullOrWhiteSpace(existingUser.PrivateKey) && !string.IsNullOrWhiteSpace(PublicKey) && !string.IsNullOrWhiteSpace(EncryptedPrivateKey))
        {
            existingUser.PublicKey = PublicKey;
            existingUser.PrivateKey = EncryptedPrivateKey;
        }

        if (!string.IsNullOrWhiteSpace(PublicKey) || !string.IsNullOrWhiteSpace(EncryptedPrivateKey))
        {
            throw new InvalidOperationException("Updated public/private key(s) were included but the user already has keys.");
        }

        return existingUser;
    }
}
