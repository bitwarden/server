#nullable enable
using System.ComponentModel.DataAnnotations;
using Bit.Core.Utilities;

namespace Bit.Core.Auth.Models.Api.Request.Accounts;

public class RegisterVerificationEmailClickedRequestModel
{
    [Required]
    [StrictEmailAddress]
    [StringLength(256)]
    public string Email { get; set; }

    [Required]
    public string EmailVerificationToken { get; set; }

}
