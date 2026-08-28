using Bit.Pam.Models;

namespace Bit.Services.Pam.Rotation.Api.Models.Response;

/// <summary>The wire shape of <see cref="PamPasswordPolicy"/>, embedded wherever a target system's policy is surfaced.</summary>
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
    /// The shortest password the daemon may generate.
    /// </summary>
    public int MinLength { get; }

    /// <summary>
    /// The longest password the daemon may generate.
    /// </summary>
    public int MaxLength { get; }

    /// <summary>
    /// Whether generated passwords include uppercase letters.
    /// </summary>
    public bool IncludeUppercase { get; }

    /// <summary>
    /// Whether generated passwords include lowercase letters.
    /// </summary>
    public bool IncludeLowercase { get; }

    /// <summary>
    /// Whether generated passwords include digits.
    /// </summary>
    public bool IncludeDigits { get; }

    /// <summary>
    /// Whether generated passwords include symbols.
    /// </summary>
    public bool IncludeSymbols { get; }
}
