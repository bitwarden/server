using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;
using Bit.Core.Utilities;

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
            if (string.IsNullOrWhiteSpace(existingUser.PublicKey))
            {
                throw new InvalidOperationException("Must set both public and private key(s) at the same time, received only private key.");
            }
            else if (string.IsNullOrWhiteSpace(existingUser.PrivateKey))
            {
                throw new InvalidOperationException("Must set both public and private key(s) at the same time, received only public key.");
            }
            else
            {
                if (!(PublicKey == existingUser.PublicKey && CoreHelpers.FixedTimeEquals(EncryptedPrivateKey, existingUser.PrivateKey)))
                {
                    throw new InvalidOperationException("Cannot replace existing key(s) with new key(s).");
                }
            }
        }

        return existingUser;
    }
}
