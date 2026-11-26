namespace Bit.AppHost.Configuration;

/// <summary>
/// Settings for the web vault npm watch build.
/// </summary>
internal sealed record WebFrontendSettings
{
    /// <summary>
    /// HTTPS port in cloud mode; self-hosted mode adds one.
    /// </summary>
    internal required int Port { get; init; }

    /// <summary>
    /// Base URL shown on the dashboard, without the port.
    /// </summary>
    internal required string Url { get; init; }
}
