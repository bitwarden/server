using System.Text.Json;

namespace Bit.Pam.Models;

/// <summary>
/// The password-generation policy stored in <see cref="Entities.PamTargetSystem.PasswordPolicy"/>. The access
/// connector generates candidate passwords against it.
/// </summary>
public record PamPasswordPolicy
{
    public required int MinLength { get; init; }
    public required int MaxLength { get; init; }
    public bool IncludeUppercase { get; init; }
    public bool IncludeLowercase { get; init; }
    public bool IncludeDigits { get; init; }
    public bool IncludeSymbols { get; init; }

    public static string Serialize(PamPasswordPolicy policy) => JsonSerializer.Serialize(policy);

    /// <summary>Null or blank in, null out.</summary>
    public static PamPasswordPolicy? Parse(string? json) =>
        string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<PamPasswordPolicy>(json);
}
