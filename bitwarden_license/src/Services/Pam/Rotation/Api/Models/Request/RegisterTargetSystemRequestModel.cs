using System.ComponentModel.DataAnnotations;
using Bit.Pam.Enums;

namespace Bit.Services.Pam.Rotation.Api.Models.Request;

/// <summary>
/// Registers a target system, automatic or manual (spec <c>RegisterAutomaticTargetSystem</c> /
/// <c>RegisterManualTargetSystem</c>) -- method-discriminated on <see cref="Method"/>: an
/// <see cref="PamTargetSystemMethod.Automatic"/> target requires <see cref="Kind"/>, <see cref="PasswordPolicy"/>,
/// and <see cref="SupportsSessionTermination"/>; a <see cref="PamTargetSystemMethod.Manual"/> target requires all
/// three to be absent. <c>RegisterTargetSystemCommand</c> re-checks this shape server-side as defense in depth
/// (see <see cref="Bit.Services.Pam.Rotation.Commands.RegisterTargetSystemCommand"/>); this validation exists so a
/// shape mismatch comes back as a field-level 400 instead of a generic one.
/// </summary>
public class RegisterTargetSystemRequestModel : IValidatableObject
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = null!;

    [Required]
    public PamTargetSystemMethod Method { get; set; }

    /// <summary>The automatic connector kind. Required when <see cref="Method"/> is Automatic; must be absent otherwise.</summary>
    public PamTargetSystemKind? Kind { get; set; }

    /// <summary>Required when <see cref="Method"/> is Automatic; must be absent otherwise.</summary>
    public PamPasswordPolicyRequestModel? PasswordPolicy { get; set; }

    /// <summary>Required when <see cref="Method"/> is Automatic; must be absent otherwise.</summary>
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
