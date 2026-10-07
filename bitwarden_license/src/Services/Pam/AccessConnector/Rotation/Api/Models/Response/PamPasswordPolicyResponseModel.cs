using Bit.Pam.Models;

namespace Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Response;

/// <summary>A target system's password-generation constraints.</summary>
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

    public int MinLength { get; set; }

    public int MaxLength { get; set; }

    public bool IncludeUppercase { get; set; }

    public bool IncludeLowercase { get; set; }

    public bool IncludeDigits { get; set; }

    public bool IncludeSymbols { get; set; }
}
