using System.ComponentModel.DataAnnotations;
using Bit.Core.KeyManagement.Models.Api.Request;
using Bit.Core.Utilities;

namespace Bit.Api.KeyManagement.Models.Requests;

public class RotateUserAccountKeysAndDataRequestModel
{
    [StringLength(300)]
    public required string OldMasterKeyAuthenticationHash { get; set; }
    public required UnlockDataRequestModel AccountUnlockData { get; set; }
    public required AccountKeysRequestModel AccountKeys { get; set; }
    public required AccountDataRequestModel AccountData { get; set; }

    // The Key ID of the key that is being rotated to.
    [KeyId]
    public string? NewUserKeyId { get; set; }
}
