using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Auth.Models.Request.Accounts;

public class VerifyOTPRequestModel
{
    [Required]
    public string OTP { get; set; }
}
