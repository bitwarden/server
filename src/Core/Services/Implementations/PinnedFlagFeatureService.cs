using System.Text.Json.Nodes;
using Bitwarden.Server.Sdk.Features;

namespace Bit.Core.Services.Implementations;

/// <summary>
/// pam/uat only - do not carry this to main. Reports a fixed value for a handful of flags and
/// delegates everything else, so the branch can pin a flag against whatever LaunchDarkly says.
/// </summary>
/// <remarks>
/// A <see cref="FeatureFlagOptions.FlagValues"/> entry cannot do this: flag values only feed the
/// data source when no LaunchDarkly SdkKey is set, so a connected instance ignores them. Pinning
/// here covers both the single checks and
/// <see cref="Bitwarden.Server.Sdk.Features.IFeatureService.GetAll"/>, which is what the clients
/// read through <c>/config</c>.
/// </remarks>
public class PinnedFlagFeatureService : Bitwarden.Server.Sdk.Features.IFeatureService
{
    // Fully qualified throughout: the obsolete Bit.Core.Services.IFeatureService sits in the
    // parent namespace and would win over the using directive, as in DelegatingFeatureService.
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

    // A pinned flag has no variation to report, so the caller's default stands in.
    public int GetIntVariation(string key, int defaultValue = 0) =>
        _pinned.ContainsKey(key) ? defaultValue : _inner.GetIntVariation(key, defaultValue);

    public string? GetStringVariation(string key, string? defaultValue = null) =>
        _pinned.ContainsKey(key) ? defaultValue : _inner.GetStringVariation(key, defaultValue);

    public IReadOnlyDictionary<string, JsonValue> GetAll()
    {
        var all = new Dictionary<string, JsonValue>(_inner.GetAll());

        // Assigned rather than added only when present: the clients fall back to their own
        // default for a flag the server omits, so a pinned flag has to be stated outright.
        foreach (var (key, value) in _pinned)
        {
            all[key] = JsonValue.Create(value);
        }

        return all;
    }
}
