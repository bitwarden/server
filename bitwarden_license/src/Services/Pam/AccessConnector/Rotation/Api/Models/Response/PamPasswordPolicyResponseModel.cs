using Bit.Pam.Models;

namespace Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Response;

/// <summary>
/// A target system's password-generation constraints, embedded wherever that policy is surfaced.
/// </summary>
public class PamPasswordPolicyResponseModel
{
    public PamPasswordPolicyResponseModel(PamPasswordPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        MinLength = policy.MinLength;
        MaxLength = policy.MaxLength;
        IncludeUppercase = policy.IncludeUppercase;
        IncludeLowercase = policy.IncludeLowercase;
        IncludeDigits = policy.IncludeDigits;
        IncludeSymbols = policy.IncludeSymbols;
    }

    /// <summary>
    /// The shortest password the access connector may generate.
    /// </summary>
    public int MinLength { get; set; }

    /// <summary>
    /// The longest password the access connector may generate.
    /// </summary>
    public int MaxLength { get; set; }

    /// <summary>
    /// Whether generated passwords include uppercase letters.
    /// </summary>
    public bool IncludeUppercase { get; set; }

    /// <summary>
    /// Whether generated passwords include lowercase letters.
    /// </summary>
    public bool IncludeLowercase { get; set; }

    /// <summary>
    /// Whether generated passwords include digits.
    /// </summary>
    public bool IncludeDigits { get; set; }

    /// <summary>
    /// Whether generated passwords include symbols.
    /// </summary>
    public bool IncludeSymbols { get; set; }
}
