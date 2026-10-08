using System.ComponentModel.DataAnnotations;

namespace Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Request;

/// <summary>
/// The updatable shape of a target system (spec <c>UpdateAutomaticTargetSystem</c> /
/// <c>UpdateManualTargetSystem</c>). Method and kind are fixed at registration, so the handler checks this shape
/// against the stored method.
/// </summary>
public class UpdateTargetSystemRequestModel
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = null!;

    /// <summary>
    /// Replaces the stored policy wholesale. Required for an automatic target; omitting it on a manual target keeps
    /// the stored policy.
    /// </summary>
    public PamPasswordPolicyRequestModel? PasswordPolicy { get; set; }

    /// <summary>
    /// Whether the integration can terminate live sessions after a rotation; configs on this target can request
    /// termination only when true. Required for an automatic target.
    /// </summary>
    public bool? SupportsSessionTermination { get; set; }
}
