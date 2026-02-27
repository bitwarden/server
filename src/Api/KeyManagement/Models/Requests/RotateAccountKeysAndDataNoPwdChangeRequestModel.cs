using Bit.Core.KeyManagement.Models.Api.Request;

namespace Bit.Api.KeyManagement.Models.Requests;

public class RotateAccountKeysAndDataNoPwdChangeRequestModel
{
    public required UnlockDataNoPwdChangeRequestModel AccountUnlockData { get; set; }
    public required AccountKeysRequestModel AccountKeys { get; set; }
    public required AccountDataRequestModel AccountData { get; set; }
}
