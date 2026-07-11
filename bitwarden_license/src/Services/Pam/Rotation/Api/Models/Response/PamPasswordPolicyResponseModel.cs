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

    public int MinLength { get; }
    public int MaxLength { get; }
    public bool IncludeUppercase { get; }
    public bool IncludeLowercase { get; }
    public bool IncludeDigits { get; }
    public bool IncludeSymbols { get; }
}
