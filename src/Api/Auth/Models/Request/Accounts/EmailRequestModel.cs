// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;
using Bit.Core.Utilities;

namespace Bit.Api.Auth.Models.Request.Accounts;

public class EmailRequestModel : SecretVerificationRequestModel
{
    [Required]
    [StrictEmailAddress]
    [StringLength(256)]
    public string NewEmail { get; set; }
    // Optional at the model level; the legacy /accounts/email path still requires it and validates
    // explicitly when the PM30806_SelfServiceChangeEmailCommand feature flag is off.
    [StringLength(300)]
    public string NewMasterPasswordHash { get; set; }
    [Required]
    public string Token { get; set; }
    // Optional at the model level; same legacy-path validation as NewMasterPasswordHash.
    public string Key { get; set; }
}
