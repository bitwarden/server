using System.ComponentModel.DataAnnotations;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Utilities;

namespace Bit.Core.KeyManagement.Models.Api.Request;

/// <summary>
/// Use this datatype when interfacing with requests to create a separation of concern.
/// See <see cref="MasterPasswordUnlockData"/> to use for commands, queries, services.
/// </summary>
public class MasterPasswordUnlockDataRequestModel
{
    public required KdfRequestModel Kdf { get; init; }
    [EncryptedString] public required string MasterKeyWrappedUserKey { get; init; }
    [StringLength(256)] public required string Salt { get; init; }

    public MasterPasswordUnlockData ToData()
    {
        return new MasterPasswordUnlockData
        {
            Kdf = Kdf.ToData(),
            MasterKeyWrappedUserKey = MasterKeyWrappedUserKey,
            Salt = Salt
        };
    }
}
