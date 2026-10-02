using Bit.Core.Utilities;

namespace Bit.Api.KeyManagement.Models.Requests;

public class RotateUserKeysRequestModel
{
    public required WrappedAccountCryptographicStateRequestModel WrappedAccountCryptographicState { get; set; }
    public required CommonUnlockDataRequestModel UnlockData { get; set; }
    public required AccountDataRequestModel AccountData { get; set; }
    public required UnlockMethodRequestModel UnlockMethodData { get; set; }

    // The Key ID of the key that is being rotated to.
    [KeyId]
    public string? NewUserKeyId { get; set; }
}
