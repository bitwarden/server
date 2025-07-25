using System.ComponentModel.DataAnnotations;
using Bit.Core.Enums;
using Bit.Core.Utilities;

namespace Bit.Core.KeyManagement.Models.Response;

public class MasterPasswordUnlockResponseModel
{
    public required MasterPasswordUnlockKdfResponseModel Kdf { get; init; }
    [EncryptedString] public required string MasterKeyEncryptedUserKey { get; init; }
    [StringLength(256)] public required string Salt { get; init; }
}

public class MasterPasswordUnlockKdfResponseModel
{
    public required KdfType KdfType { get; init; }
    public required int Iterations { get; init; }
    public int? Memory { get; init; }
    public int? Parallelism { get; init; }
}
