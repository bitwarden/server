using System.ComponentModel.DataAnnotations;
using Bit.Pam.Enums;

namespace Bit.Services.Pam.Rotation.Api.Models.Request;

/// <summary>
/// Registers a target system, automatic or manual (spec <c>RegisterAutomaticTargetSystem</c> /
/// <c>RegisterManualTargetSystem</c>) -- method-discriminated on <see cref="Method"/>: an
/// <see cref="PamTargetSystemMethod.Automatic"/> target requires <see cref="Kind"/>, <see cref="PasswordPolicy"/>,
/// and <see cref="SupportsSessionTermination"/>; a <see cref="PamTargetSystemMethod.Manual"/> target requires all
/// three to be absent. <c>RegisterTargetSystemCommand</c> re-checks this shape server-side as defense in depth;
/// this validation exists so a shape mismatch comes back as a field-level 400 instead of a generic one.
/// </summary>
public class RegisterTargetSystemRequestModel : IValidatableObject
{
    /// <summary>The target system's display name, shown wherever targets are listed and managed.</summary>
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = null!;

    /// <summary>
    /// How the target's credentials are rotated -- by a rotation daemon (automatic) or by a human out of band
    /// (manual). Decides which of the remaining fields apply.
    /// </summary>
    [Required]
    public PamTargetSystemMethod Method { get; set; }

    /// <summary>The connector an automatic target is rotated through.</summary>
    public PamTargetSystemKind? Kind { get; set; }

    /// <summary>
    /// The password-generation constraints the daemon must satisfy when rotating credentials on this target.
    /// </summary>
    public PamPasswordPolicyRequestModel? PasswordPolicy { get; set; }

    /// <summary>
    /// Whether the connector can terminate the account's live sessions after a rotation; gates whether rotation
    /// configs on this target may request session termination.
    /// </summary>
    public bool? SupportsSessionTermination { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Method == PamTargetSystemMethod.Automatic)
        {
            if (Kind is null)
            {
                yield return new ValidationResult(
                    "Kind is required for an automatic target system.", [nameof(Kind)]);
            }

            if (PasswordPolicy is null)
            {
                yield return new ValidationResult(
                    "PasswordPolicy is required for an automatic target system.", [nameof(PasswordPolicy)]);
            }

            if (SupportsSessionTermination is null)
            {
                yield return new ValidationResult(
                    "SupportsSessionTermination is required for an automatic target system.",
                    [nameof(SupportsSessionTermination)]);
            }
        }
        else
        {
            if (Kind is not null)
            {
                yield return new ValidationResult(
                    "Kind must not be set for a manual target system.", [nameof(Kind)]);
            }

            if (PasswordPolicy is not null)
            {
                yield return new ValidationResult(
                    "PasswordPolicy must not be set for a manual target system.", [nameof(PasswordPolicy)]);
            }

            if (SupportsSessionTermination is not null)
            {
                yield return new ValidationResult(
                    "SupportsSessionTermination must not be set for a manual target system.",
                    [nameof(SupportsSessionTermination)]);
            }
        }
    }
}
