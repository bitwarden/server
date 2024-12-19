using System.ComponentModel.DataAnnotations;
using Bit.Core.Utilities;

namespace Bit.Api.Auth.Models.Request.Accounts;

public class UnauthenticatedSecretVerificatioRequestModel : SecretVerificationRequestModel
{
    [Required]
    [StrictEmailAddress]
    [StringLength(256)]
    public string Email { get; set; }
}
