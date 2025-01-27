using System.ComponentModel.DataAnnotations;

namespace Bit.Api.KeyManagement.Models.Request.Accounts;

public class RotateUserAccountKeysAndDataRequestModel
{
    [Required]
    public string OldMasterKeyAuthenticationHash { get; set; }
    [Required]
    public UnlockDataRequestModel AccountUnlockData { get; set; }
    [Required]
    public AccountKeysRequestModel AccountKeys { get; set; }
    [Required]
    public AccountDataRequestModel AccountData { get; set; }
}
