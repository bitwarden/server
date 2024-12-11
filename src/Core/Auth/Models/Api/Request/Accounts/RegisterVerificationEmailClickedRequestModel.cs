#nullable enable
using System.ComponentModel.DataAnnotations;
using Bit.Core.Utilities;

namespace Bit.Core.Auth.Models.Api.Request.Accounts;

public class RegisterVerificationEmailClickedRequestModel
{
    [StrictEmailAddress]
    [StringLength(256)]
    public required string Email { get; set; }

    public required string EmailVerificationToken { get; set; }
}
