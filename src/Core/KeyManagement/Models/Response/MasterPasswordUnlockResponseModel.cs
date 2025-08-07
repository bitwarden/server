using System.ComponentModel.DataAnnotations;
using Bit.Core.Enums;
using Bit.Core.Utilities;

namespace Bit.Core.KeyManagement.Models.Response;

public class MasterPasswordUnlockResponseModel
{
    [Required] public required MasterPasswordUnlockKdfResponseModel Kdf { get; init; }
    [Required][EncryptedString] public required string MasterKeyEncryptedUserKey { get; init; }
    [Required][StringLength(256)] public required string Salt { get; init; }
}

public class MasterPasswordUnlockKdfResponseModel
{
    [Required] public required KdfType KdfType { get; init; }
    [Required] public required int Iterations { get; init; }
    public int? Memory { get; init; }
    public int? Parallelism { get; init; }
}
