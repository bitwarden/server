using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Auth.Models.Request.Accounts;

public class SetVerifyDevicesRequestModel : SecretVerificationRequestModel
{
    [Required]
    public bool VerifyDevices { get; set; }
}
