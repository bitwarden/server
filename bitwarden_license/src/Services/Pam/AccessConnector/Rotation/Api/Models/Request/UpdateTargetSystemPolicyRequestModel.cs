using System.ComponentModel.DataAnnotations;

namespace Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Request;

/// <summary>
/// The body of <c>PUT rotation/target-systems/{id}/policy</c> (spec <c>UpdateTargetSystemPolicy</c>). Only applies to an
/// automatic target system.
/// </summary>
public class UpdateTargetSystemPolicyRequestModel
{
    /// <summary>
    /// The replacement password-generation constraints the access connector must satisfy when rotating credentials on
    /// this target; overwrites the stored policy wholesale.
    /// </summary>
    [Required]
    public PamPasswordPolicyRequestModel PasswordPolicy { get; set; } = null!;

    /// <summary>
    /// Whether the integration can terminate the account's live sessions after a rotation; gates whether rotation
    /// configs on this target may request session termination.
    /// </summary>
    [Required]
    public bool SupportsSessionTermination { get; set; }
}
