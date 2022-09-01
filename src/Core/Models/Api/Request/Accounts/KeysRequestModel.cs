using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;

namespace Bit.Core.Models.Api.Request.Accounts;

public class KeysRequestModel
{
    public string PublicKey { get; set; }
    [Required]
    public string EncryptedPrivateKey { get; set; }

    public User ToUser(User existingUser)
    {
        if (string.IsNullOrWhiteSpace(existingUser.PublicKey) && !string.IsNullOrWhiteSpace(PublicKey))
        {
            existingUser.PublicKey = PublicKey;
        }

        if (string.IsNullOrWhiteSpace(existingUser.PrivateKey))
        {
            existingUser.PrivateKey = EncryptedPrivateKey;
        }

        return existingUser;
    }
}
