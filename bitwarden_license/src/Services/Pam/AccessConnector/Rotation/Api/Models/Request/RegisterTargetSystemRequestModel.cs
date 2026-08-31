using System.ComponentModel.DataAnnotations;
using Bit.Pam.Enums;

namespace Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Request;

/// <summary>
/// Registers a target system, automatic or manual (spec <c>RegisterAutomaticTargetSystem</c> /
/// <c>RegisterManualTargetSystem</c>) -- method-discriminated on <see cref="Method"/>: an
/// <see cref="PamTargetSystemMethod.Automatic"/> target carries <see cref="Kind"/>, <see cref="PasswordPolicy"/>,
/// and <see cref="SupportsSessionTermination"/>; a <see cref="PamTargetSystemMethod.Manual"/> target carries none
/// of the three.
/// </summary>
public class RegisterTargetSystemRequestModel : IValidatableObject
{
    /// <summary>The target system's display name, shown wherever targets are listed and managed.</summary>
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = null!;

    /// <summary>
    /// How the target's credentials are rotated -- by an access connector (automatic) or by a human out of band
    /// (manual). Decides which of the remaining fields apply. Nullable so an omitted value is rejected rather than
    /// binding to <see cref="PamTargetSystemMethod.Automatic"/>, which would register an automatic target for a
    /// caller who never asked for one.
    /// </summary>
    [Required]
    [EnumDataType(typeof(PamTargetSystemMethod))]
    public PamTargetSystemMethod? Method { get; set; }

    /// <summary>The integration an automatic target is rotated through.</summary>
    [EnumDataType(typeof(PamTargetSystemKind))]
    public PamTargetSystemKind? Kind { get; set; }

    /// <summary>
    /// The password-generation constraints the access connector must satisfy when rotating credentials on this target.
    /// </summary>
    public PamPasswordPolicyRequestModel? PasswordPolicy { get; set; }

    /// <summary>
    /// Whether the integration can terminate the account's live sessions after a rotation; gates whether rotation
    /// configs on this target may request session termination.
    /// </summary>
    public bool? SupportsSessionTermination { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Method is null)
        {
            // [Required] reports the omission on its own. Falling through would additionally report the manual
            // shape rules, blaming fields the caller never had the chance to get wrong.
            yield break;
        }

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
