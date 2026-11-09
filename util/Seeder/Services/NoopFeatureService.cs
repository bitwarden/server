using System.Text.Json.Nodes;

namespace Bit.Seeder.Services;

/// <summary>
/// Returns every caller's own default. Satisfies the hard constructor dependencies the Core billing graph
/// has on feature services (e.g. <c>PriceIncreaseScheduler</c>) without pulling the LaunchDarkly-backed
/// SDK implementation into a CLI tool.
/// </summary>
public sealed class NoopFeatureService : Bitwarden.Server.Sdk.Features.IFeatureService
{
    public bool IsEnabled(string key, bool defaultValue = false) => defaultValue;

    public int GetIntVariation(string key, int defaultValue = 0) => defaultValue;

    public string? GetStringVariation(string key, string? defaultValue = null) => defaultValue;

    public IReadOnlyDictionary<string, JsonValue> GetAll() => new Dictionary<string, JsonValue>();
}
