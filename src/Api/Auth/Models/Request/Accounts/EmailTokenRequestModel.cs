using System.ComponentModel.DataAnnotations;
using Bit.Core.Utilities;

namespace Bit.Api.Models.Request.Accounts;

public class EmailTokenRequestModel : SecretVerificationRequestModel
{
    [Required]
    [StrictEmailAddress]
    [StringLength(256)]
    public string NewEmail { get; set; }
}
