using Bit.Core.Services;

namespace Bit.Seeder.Services;

/// <summary>
/// Implementation of <see cref="IFeatureService"/> that always returns the default value.
/// Used by the Seeder so we don't pull in LaunchDarkly for test-data generation; the billing
/// pipeline calls <see cref="IFeatureService"/> for tax/feature gating that doesn't affect the
/// trial-subscription path.
/// </summary>
public sealed class NoOpFeatureService : IFeatureService
{
    public bool IsOnline() => false;

    public bool IsEnabled(string key, bool defaultValue = false) => defaultValue;

    public int GetIntVariation(string key, int defaultValue = 0) => defaultValue;

    public string GetStringVariation(string key, string defaultValue = null!) => defaultValue;

    public Dictionary<string, object> GetAll() => new();
}
