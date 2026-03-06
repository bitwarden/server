using System.ComponentModel.DataAnnotations;
using Bit.Core.KeyManagement.Models.Api.Request;

namespace Bit.Api.Auth.Models.Request.Accounts;

public class UpdateTdeOffboardingPasswordRequestModel
{
    [StringLength(300)]
    public string? NewMasterPasswordHash { get; set; }
    public string? Key { get; set; }

    [Required]
    [StringLength(50)]
    public required string MasterPasswordHint { get; set; }

    public MasterPasswordAuthenticationDataRequestModel? AuthenticationData { get; set; }
    public MasterPasswordUnlockDataRequestModel? UnlockData { get; set; }
}
