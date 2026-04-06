namespace Bit.Api.KeyManagement.Models.Requests;

public class RotateUserKeysRequestModel
{
    public required WrappedAccountCryptographicStateRequestModel WrappedAccountCryptographicState { get; set; }
    public required CommonUnlockDataRequestModel UnlockData { get; set; }
    public required AccountDataRequestModel AccountData { get; set; }
    public required UnlockMethodRequestModel UnlockMethodData { get; set; }
}
