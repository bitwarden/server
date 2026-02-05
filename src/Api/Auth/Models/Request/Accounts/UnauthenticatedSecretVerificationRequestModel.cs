// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;
using Bit.Core.Utilities;

namespace Bit.Api.Auth.Models.Request.Accounts;

public class UnauthenticatedSecretVerificationRequestModel : SecretVerificationRequestModel
{
    [Required]
    [StrictEmailAddress]
    [StringLength(256)]
    public string Email { get; set; }
}
