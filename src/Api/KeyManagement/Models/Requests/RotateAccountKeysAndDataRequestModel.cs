#nullable enable
using Bit.Api.KeyManagement.Models.Requests;

namespace Bit.Api.KeyManagement.Models.Request;

public class RotateUserAccountKeysAndDataRequestModel
{
    public required string OldMasterKeyAuthenticationHash { get; set; }
    public required UnlockDataRequestModel AccountUnlockData { get; set; }
    public required AccountKeysRequestModel AccountKeys { get; set; }
    public required AccountDataRequestModel AccountData { get; set; }
}
