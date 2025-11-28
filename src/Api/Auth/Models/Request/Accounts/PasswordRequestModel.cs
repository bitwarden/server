#nullable enable

using System.ComponentModel.DataAnnotations;
using Bit.Api.KeyManagement.Models.Requests;

namespace Bit.Api.Auth.Models.Request.Accounts;

public class PasswordRequestModel : SecretVerificationRequestModel
{
    [Required]
    [StringLength(300)]
    public required string NewMasterPasswordHash { get; set; }
    [StringLength(50)]
    public string? MasterPasswordHint { get; set; }
    [Required]
    public required string Key { get; set; }

    // Note: These will eventually become required, but not all consumers are moved over yet.
    public MasterPasswordAuthenticationDataRequestModel? AuthenticationData { get; set; }
    public MasterPasswordUnlockDataRequestModel? UnlockData { get; set; }
}
