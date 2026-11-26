namespace Bit.AppHost.Configuration;

/// <summary>
/// Settings for the Azurite storage emulator.
/// </summary>
internal sealed record AzuriteSettings
{
    /// <summary>
    /// Folder mounted as the emulator's data, relative to the AppHost directory; empty keeps Aspire's
    /// per-checkout data volume.
    /// </summary>
    internal string DataPath { get; init; } = "";
}
