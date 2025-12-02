// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;
using Bit.Core.KeyManagement.Models.Api.Request;
using Bit.Core.Utilities;

namespace Bit.Core.Auth.Models.Api.Request.Accounts;

public class KeysRequestModel
{
    [Required]
    public string PublicKey { get; set; }
    [Required]
    public string EncryptedPrivateKey { get; set; }
    public AccountKeysRequestModel accountKeys { get; set; }

    public User ToUser(User existingUser)
    {
        if (string.IsNullOrWhiteSpace(PublicKey) || string.IsNullOrWhiteSpace(EncryptedPrivateKey))
        {
            throw new InvalidOperationException("Public and private keys are required.");
        }

        if (string.IsNullOrWhiteSpace(existingUser.PublicKey) && string.IsNullOrWhiteSpace(existingUser.PrivateKey))
        {
            existingUser.PublicKey = PublicKey;
            existingUser.PrivateKey = EncryptedPrivateKey;
            return existingUser;
        }
        else if (PublicKey == existingUser.PublicKey && CoreHelpers.FixedTimeEquals(EncryptedPrivateKey, existingUser.PrivateKey))
        {
            return existingUser;
        }
        else
        {
            throw new InvalidOperationException("Cannot replace existing key(s) with new key(s).");
        }
    }
}
