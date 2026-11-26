namespace Bit.AppHost.Configuration;

/// <summary>
/// File names of the PowerShell scripts the AppHost runs.
/// </summary>
internal sealed record ScriptSettings
{
    /// <summary>
    /// Applies database migrations.
    /// </summary>
    internal required string DbMigration { get; init; }

    /// <summary>
    /// Creates the Azurite blob containers, queues and tables.
    /// </summary>
    internal required string AzuriteSetup { get; init; }

    /// <summary>
    /// Copies <c>dev/secrets.json</c> into each project's user secrets.
    /// </summary>
    internal required string SecretsSetup { get; init; }
}
