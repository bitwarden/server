using System.ComponentModel.DataAnnotations;
using Bit.Pam.Models;

namespace Bit.Services.Pam.Rotation.Api.Models.Request;

/// <summary>
/// The wire shape of <see cref="PamPasswordPolicy"/> -- the password-generation constraints an automatic target
/// system's rotation daemon must satisfy. Embedded in <see cref="RegisterTargetSystemRequestModel"/> and
/// <see cref="UpdateTargetSystemPolicyRequestModel"/>.
/// </summary>
public class PamPasswordPolicyRequestModel : IValidatableObject
{
    [Required]
    [Range(1, 128)]
    public int MinLength { get; set; }

    [Required]
    [Range(1, 128)]
    public int MaxLength { get; set; }

    public bool IncludeUppercase { get; set; }
    public bool IncludeLowercase { get; set; }
    public bool IncludeDigits { get; set; }
    public bool IncludeSymbols { get; set; }

    public PamPasswordPolicy ToPasswordPolicy() => new()
    {
        MinLength = MinLength,
        MaxLength = MaxLength,
        IncludeUppercase = IncludeUppercase,
        IncludeLowercase = IncludeLowercase,
        IncludeDigits = IncludeDigits,
        IncludeSymbols = IncludeSymbols,
    };

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MinLength > MaxLength)
        {
            yield return new ValidationResult(
                "MinLength must not be greater than MaxLength.", [nameof(MinLength), nameof(MaxLength)]);
        }

        if (!IncludeUppercase && !IncludeLowercase && !IncludeDigits && !IncludeSymbols)
        {
            yield return new ValidationResult(
                "At least one character class must be included.",
                [nameof(IncludeUppercase), nameof(IncludeLowercase), nameof(IncludeDigits), nameof(IncludeSymbols)]);
        }
    }
}
