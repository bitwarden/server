using Bit.Core.Services;

namespace Bit.Seeder.Services;

/// <summary>
/// Returns every caller's own default. Satisfies the hard constructor dependency the Core billing graph
/// still has on the obsolete <see cref="IFeatureService"/> (via <c>PriceIncreaseScheduler</c>) without
/// pulling the LaunchDarkly-backed SDK implementation into a CLI tool.
/// </summary>
/// <remarks>
/// The obsolete interface is the one that must be implemented here — it is what the billing services
/// actually ask for. Do not "fix" this to <c>Bitwarden.Server.Sdk.Features.IFeatureService</c>.
/// </remarks>
public sealed class NoopFeatureService : IFeatureService
{
    public bool IsEnabled(string key, bool defaultValue = false) => defaultValue;

    public int GetIntVariation(string key, int defaultValue = 0) => defaultValue;

    public string? GetStringVariation(string key, string? defaultValue = null) => defaultValue;
}
