using System.Text.Json;

namespace Bit.Pam.Models;

/// <summary>
/// The password-generation policy for an automatic <see cref="Entities.PamTargetSystem"/>, persisted as the JSON
/// document in <see cref="Entities.PamTargetSystem.PasswordPolicy"/>. The rotation daemon generates a candidate
/// password against these constraints before writing it to the target.
/// </summary>
public record PamPasswordPolicy
{
    public required int MinLength { get; init; }
    public required int MaxLength { get; init; }
    public bool IncludeUppercase { get; init; }
    public bool IncludeLowercase { get; init; }
    public bool IncludeDigits { get; init; }
    public bool IncludeSymbols { get; init; }

    /// <summary>Serializes a policy for storage in <see cref="Entities.PamTargetSystem.PasswordPolicy"/>.</summary>
    public static string Serialize(PamPasswordPolicy policy) => JsonSerializer.Serialize(policy);

    /// <summary>Deserializes a target system's stored <c>PasswordPolicy</c> JSON. Null in, null out.</summary>
    public static PamPasswordPolicy? Parse(string? json) =>
        string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<PamPasswordPolicy>(json);
}
