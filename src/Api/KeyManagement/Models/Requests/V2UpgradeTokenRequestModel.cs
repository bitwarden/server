using System.ComponentModel.DataAnnotations;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Utilities;

namespace Bit.Api.KeyManagement.Models.Requests;

/// <summary>
/// Request model for V2 upgrade token submitted during key rotation.
/// Contains wrapped user keys allowing clients to unlock after V1→V2 upgrade.
/// </summary>
public class V2UpgradeTokenRequestModel
{
    /// <summary>
    /// User Key V2 Wrapped User Key V1.
    /// </summary>
    [Required]
    [EncryptedString]
    public required string WrappedUserKey1 { get; init; }

    /// <summary>
    /// User Key V1 Wrapped User Key V2.
    /// </summary>
    [Required]
    [EncryptedString]
    public required string WrappedUserKey2 { get; init; }

    public V2UpgradeTokenData ToData()
    {
        return new V2UpgradeTokenData
        {
            WrappedUserKey1 = WrappedUserKey1,
            WrappedUserKey2 = WrappedUserKey2
        };
    }
}
