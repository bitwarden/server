// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;
using Bit.Core.Utilities;

namespace Bit.Api.Auth.Models.Request.Accounts;

/// <summary>
/// This model is used in the second step of self service email change after the master password hash has been verified.
/// The token is used to verify ownership of the new email.
/// </summary>
public class EmailRequestModel : SecretVerificationRequestModel
{
    [Required]
    [StrictEmailAddress]
    [StringLength(256)]
    public string NewEmail { get; set; }
    // Optional at the model level; the legacy /accounts/email path still requires it and validates
    // explicitly when the PM30806_SelfServiceChangeEmailCommand feature flag is off.
    // TODO: PM-39120 - PM30806_SelfServiceChangeEmailCommand flag cleanup. The MasterPasswordHash
    // is no longer needed since the MasterPasswordHash will not be updated. This field was used to update
    // the user.MasterPassword in the legacy flow.
    [StringLength(300)]
    public string NewMasterPasswordHash { get; set; }
    [Required]
    public string Token { get; set; }
    // Optional at the model level; same legacy-path validation as NewMasterPasswordHash.
    // TODO: PM-39120 - PM30806_SelfServiceChangeEmailCommand flag cleanup. Keys are no longer rotated
    // this field can be removed.
    public string Key { get; set; }
}
