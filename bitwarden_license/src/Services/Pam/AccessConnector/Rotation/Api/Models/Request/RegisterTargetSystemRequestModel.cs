using System.ComponentModel.DataAnnotations;
using Bit.Pam.Enums;

namespace Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Request;

/// <summary>
/// Registers a target system (spec <c>RegisterAutomaticTargetSystem</c> / <c>RegisterManualTargetSystem</c>). An
/// automatic target carries <see cref="Kind"/>, <see cref="PasswordPolicy"/> and
/// <see cref="SupportsSessionTermination"/>; a manual one at most a <see cref="PasswordPolicy"/>.
/// </summary>
public class RegisterTargetSystemRequestModel : IValidatableObject
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = null!;

    /// <summary>
    /// Rotated by an access connector (automatic) or by a person out of band (manual). Nullable so an omitted value
    /// is rejected rather than read as <see cref="PamTargetSystemMethod.Automatic"/>.
    /// </summary>
    [Required]
    [EnumDataType(typeof(PamTargetSystemMethod))]
    public PamTargetSystemMethod? Method { get; set; }

    /// <summary>The integration an automatic target is rotated through.</summary>
    [EnumDataType(typeof(PamTargetSystemKind))]
    public PamTargetSystemKind? Kind { get; set; }

    /// <summary>Required for an automatic target; unenforced operator guidance on a manual one.</summary>
    public PamPasswordPolicyRequestModel? PasswordPolicy { get; set; }

    /// <summary>
    /// Whether the integration can terminate live sessions after a rotation; configs on this target can request
    /// termination only when true.
    /// </summary>
    public bool? SupportsSessionTermination { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Method is null)
        {
            // [Required] reports the omission; falling through would also blame fields under the manual rules.
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

            if (SupportsSessionTermination is not null)
            {
                yield return new ValidationResult(
                    "SupportsSessionTermination must not be set for a manual target system.",
                    [nameof(SupportsSessionTermination)]);
            }
        }
    }
}
