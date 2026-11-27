namespace Bit.AppHost.Configuration;

/// <summary>
/// Settings for one Bitwarden service.
/// </summary>
internal sealed record ServiceSettings
{
    /// <summary>
    /// Host port of the service's <c>http</c> endpoint in cloud mode; self-hosted mode adds one.
    /// </summary>
    internal required int BasePort { get; init; }
}
