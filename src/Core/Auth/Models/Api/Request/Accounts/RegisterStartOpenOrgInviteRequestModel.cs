using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Auth.Models.Api.Request.Accounts;

/// <summary>
/// Register-start payload for an open organization invite link: the {organizationId, code}
/// reference (inherited from <see cref="OpenOrgInviteRequestModel"/>) plus the opaque SDK-produced
/// sealed blob that is echoed to the verification email URL to enable the registration finish tab
/// to securely reconstitute the open organization invite data.
/// </summary>
public class RegisterStartOpenOrgInviteRequestModel : OpenOrgInviteRequestModel
{
    private const int SealedOpenOrgInviteDataMaxLength = 4096;

    [Required]
    [MaxLength(SealedOpenOrgInviteDataMaxLength)]
    public required string SealedOpenOrgInviteData { get; set; }
}
