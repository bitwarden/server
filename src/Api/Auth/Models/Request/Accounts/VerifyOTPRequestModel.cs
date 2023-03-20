using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Models.Request.Accounts;

public class VerifyOTPRequestModel
{
    [Required]
    public string OTP { get; set; }
}
