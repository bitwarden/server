using System.Text.Json.Nodes;
using Bitwarden.Server.Sdk.Features;

namespace Bit.Core.Services.Implementations;

/// <summary>
/// pam/uat only; do not carry this to main. Pins a handful of flags regardless of LaunchDarkly and
/// delegates the rest.
/// </summary>
/// <remarks>
/// A <see cref="FeatureFlagOptions.FlagValues"/> entry cannot do this, since a LaunchDarkly-connected
/// instance ignores flag values.
/// </remarks>
public class PinnedFlagFeatureService : Bitwarden.Server.Sdk.Features.IFeatureService
{
    // Fully qualified because the obsolete Bit.Core.Services.IFeatureService in the parent namespace
    // would win over the using directive.
    private readonly Bitwarden.Server.Sdk.Features.IFeatureService _inner;
    private readonly IReadOnlyDictionary<string, bool> _pinned;

    public PinnedFlagFeatureService(
        Bitwarden.Server.Sdk.Features.IFeatureService inner,
        IReadOnlyDictionary<string, bool> pinned)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(pinned);

        _inner = inner;
        _pinned = pinned;
    }

    public bool IsEnabled(string key, bool defaultValue = false) =>
        _pinned.TryGetValue(key, out var pinned) ? pinned : _inner.IsEnabled(key, defaultValue);

    // A pinned flag has no variation to report, so the caller's default is returned.
    public int GetIntVariation(string key, int defaultValue = 0) =>
        _pinned.ContainsKey(key) ? defaultValue : _inner.GetIntVariation(key, defaultValue);

    public string? GetStringVariation(string key, string? defaultValue = null) =>
        _pinned.ContainsKey(key) ? defaultValue : _inner.GetStringVariation(key, defaultValue);

    public IReadOnlyDictionary<string, JsonValue> GetAll()
    {
        var all = new Dictionary<string, JsonValue>(_inner.GetAll());

        // Set even when absent from the inner result, since the clients fall back to their own
        // default for a flag the server omits.
        foreach (var (key, value) in _pinned)
        {
            all[key] = JsonValue.Create(value);
        }

        return all;
    }
}
