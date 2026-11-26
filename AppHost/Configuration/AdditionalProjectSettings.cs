namespace Bit.AppHost.Configuration;

/// <summary>
/// An extra project added through configuration instead of code.
/// </summary>
internal sealed record AdditionalProjectSettings
{
    /// <summary>
    /// Path to the project file; the entry is skipped when empty.
    /// </summary>
    internal string? Path { get; init; }

    /// <summary>
    /// Names of the Bitwarden services that receive a reference to this project.
    /// </summary>
    internal string[] ReferencedBy { get; init; } = [];
}
