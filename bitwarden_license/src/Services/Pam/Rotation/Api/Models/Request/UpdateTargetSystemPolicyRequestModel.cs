using System.ComponentModel.DataAnnotations;

namespace Bit.Services.Pam.Rotation.Api.Models.Request;

/// <summary>
/// The body of <c>PUT target-systems/{id}/policy</c> (spec <c>UpdateTargetSystemPolicy</c>). Only valid on an
/// automatic target; <c>UpdateTargetSystemPolicyCommand</c> guards that <see cref="SupportsSessionTermination"/> may
/// only be withdrawn (true to false) when no rotation config on the target currently has <c>TerminateSessions</c> set.
/// </summary>
public class UpdateTargetSystemPolicyRequestModel
{
    [Required]
    public PamPasswordPolicyRequestModel PasswordPolicy { get; set; } = null!;

    [Required]
    public bool SupportsSessionTermination { get; set; }
}
