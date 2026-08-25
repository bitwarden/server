using System.ComponentModel.DataAnnotations;

namespace Bit.Services.Pam.Rotation.Api.Models.Request;

/// <summary>
/// The body of <c>PUT target-systems/{id}/policy</c> (spec <c>UpdateTargetSystemPolicy</c>). Only valid on an
/// automatic target; <c>UpdateTargetSystemPolicyCommand</c> guards that <see cref="SupportsSessionTermination"/> may
/// only be withdrawn (true to false) when no rotation config on the target currently has <c>TerminateSessions</c> set.
/// </summary>
public class UpdateTargetSystemPolicyRequestModel
{
    /// <summary>
    /// The replacement password-generation constraints the daemon must satisfy when rotating credentials on
    /// this target; overwrites the stored policy wholesale.
    /// </summary>
    [Required]
    public PamPasswordPolicyRequestModel PasswordPolicy { get; set; } = null!;

    /// <summary>
    /// Whether the connector can terminate the account's live sessions after a rotation; gates whether rotation
    /// configs on this target may request session termination.
    /// </summary>
    [Required]
    public bool SupportsSessionTermination { get; set; }
}
