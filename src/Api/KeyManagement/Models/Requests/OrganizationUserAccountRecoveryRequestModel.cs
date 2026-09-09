using System.ComponentModel.DataAnnotations;
using Bit.Core.Utilities;

namespace Bit.Api.KeyManagement.Models.Requests;

/// <summary>
/// An organization membership's account recovery key, submitted during key rotation.
/// </summary>
public class OrganizationUserAccountRecoveryRequestModel
{
    [Required]
    public Guid OrganizationId { get; set; }

    /// <summary>
    /// The user key encapsulated by the organization's public key. NULL during a V1 to V2 upgrade rotation,
    /// where an admin re-encapsulates it later from the upgrade token.
    /// </summary>
    [EncryptedString]
    public string? ResetPasswordKey { get; set; }
}
