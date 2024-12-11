using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Auth.Models.Request.Accounts;

public class VerifyEmailRequestModel
{
    [Required]
    public string UserId { get; set; }

    [Required]
    public string Token { get; set; }
}
