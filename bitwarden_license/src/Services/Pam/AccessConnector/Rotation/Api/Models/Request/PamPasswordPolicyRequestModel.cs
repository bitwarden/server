using System.ComponentModel.DataAnnotations;
using Bit.Pam.Models;

namespace Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Request;

/// <summary>
/// The password-generation constraints an automatic target system's access connector must satisfy. Embedded in
/// <see cref="RegisterTargetSystemRequestModel"/> and <see cref="UpdateTargetSystemRequestModel"/>.
/// </summary>
public class PamPasswordPolicyRequestModel : IValidatableObject
{
    /// <summary>The shortest password the access connector may generate.</summary>
    [Required]
    [Range(1, 128)]
    public int MinLength { get; set; }

    /// <summary>The longest password the access connector may generate.</summary>
    [Required]
    [Range(1, 128)]
    public int MaxLength { get; set; }

    /// <summary>Whether generated passwords include uppercase letters.</summary>
    public bool IncludeUppercase { get; set; }

    /// <summary>Whether generated passwords include lowercase letters.</summary>
    public bool IncludeLowercase { get; set; }

    /// <summary>Whether generated passwords include digits.</summary>
    public bool IncludeDigits { get; set; }

    /// <summary>Whether generated passwords include symbols.</summary>
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
