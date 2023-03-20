using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Models.Request.Accounts;

public class VerifyDeleteRecoverRequestModel
{
    [Required]
    public string UserId { get; set; }
    [Required]
    public string Token { get; set; }
}
