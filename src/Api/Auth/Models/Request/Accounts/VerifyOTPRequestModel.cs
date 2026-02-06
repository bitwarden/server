// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Auth.Models.Request.Accounts;

public class VerifyOTPRequestModel
{
    [Required]
    public string OTP { get; set; }
}
