#nullable enable
using Bit.Core.Entities;
using Bit.Core.Enums;

namespace Bit.Core.KeyManagement.Models.Data;

public class MasterPasswordUnlockAndAuthenticationData
{
    public KdfType KdfType { get; set; }
    public int KdfIterations { get; set; }
    public int? KdfMemory { get; set; }
    public int? KdfParallelism { get; set; }

    public required string Email { get; set; }
    public required string MasterKeyAuthenticationHash { get; set; }
    /// <summary>
    /// The user's symmetric key encrypted with their master key.
    /// Also known as "MasterKeyWrappedUserKey"
    /// </summary>
    public required string MasterKeyEncryptedUserKey { get; set; }
    public string? MasterPasswordHint { get; set; }

    public bool ValidateForUser(User user)
    {
        if (KdfType != user.Kdf || KdfMemory != user.KdfMemory || KdfParallelism != user.KdfParallelism || KdfIterations != user.KdfIterations)
        {
            return false;
        }
        else if (Email != user.Email)
        {
            return false;
        }
        else
        {
            return true;
        }
    }
}
