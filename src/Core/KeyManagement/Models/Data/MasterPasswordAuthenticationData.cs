#nullable enable

using Bit.Core.Entities;

namespace Bit.Core.KeyManagement.Models.Data;

public class MasterPasswordAuthenticationData
{
    public required KdfSettings Kdf { get; init; }
    public required string MasterPasswordAuthenticationHash { get; init; }
    public required string Salt { get; init; }

    public void ValidateSaltUnchangedForUser(User user)
    {
        if (user.GetMasterPasswordSalt() != Salt)
        {
            throw new ArgumentException("Invalid master password salt.");
        }
    }
}
