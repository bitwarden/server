#nullable enable
using System.ComponentModel.DataAnnotations;

namespace Bit.Api.KeyManagement.Models.Requests;

public class RotateUserAccountKeysAndDataRequestModel
{
    [StringLength(300)]
    public required string OldMasterKeyAuthenticationHash { get; set; }
    public required UnlockDataRequestModel AccountUnlockData { get; set; }
    public required AccountKeysRequestModel AccountKeys { get; set; }
    public required AccountDataRequestModel AccountData { get; set; }
}
