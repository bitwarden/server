using System.ComponentModel.DataAnnotations;

namespace Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Request;

/// <summary>
/// The body of <c>PUT rotation/target-systems/{id}</c> (spec <c>UpdateAutomaticTargetSystem</c> / <c>UpdateManualTargetSystem</c>), which replaces the
/// separate rename and policy operations. Carries the target's whole updatable shape: every field is written as
/// sent, so a caller editing one of them must send the others as they stand rather than omitting them. The target's
/// method and kind are fixed at registration and cannot be updated here.
/// </summary>
/// <remarks>
/// <see cref="PasswordPolicy"/> and <see cref="SupportsSessionTermination"/> belong to an automatic target and must
/// be absent on a manual one, the same shape rule <see cref="RegisterTargetSystemRequestModel"/> enforces. It cannot
/// be enforced here, because the body no longer carries the method that decides it -- the command checks the pair
/// against the stored method instead.
/// </remarks>
public class UpdateTargetSystemRequestModel
{
    /// <summary>The target system's display name.</summary>
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = null!;

    /// <summary>
    /// The replacement password-generation constraints the access connector must satisfy when rotating credentials on
    /// this target; overwrites the stored policy wholesale.
    /// </summary>
    public PamPasswordPolicyRequestModel? PasswordPolicy { get; set; }

    /// <summary>
    /// Whether the integration can terminate the account's live sessions after a rotation; gates whether rotation
    /// configs on this target may request session termination.
    /// </summary>
    public bool? SupportsSessionTermination { get; set; }
}
