using Bit.Core.Entities;

#nullable enable

namespace Bit.Core.Services;

/// <summary>
/// Reads per-device settings for the device making the current request, so server features can
/// branch on them the same way they consume feature flags. These values are user-controlled and
/// persisted per <see cref="Device"/> (each setting is a dedicated column, e.g.
/// <see cref="Device.UseNewUi"/>) — they are not feature flags and must not be read via
/// <c>IFeatureService</c>. Add a typed accessor here as each new per-device setting is introduced.
/// </summary>
public interface IDeviceSettingsService
{
    /// <summary>
    /// True when the device making the current request has opted into the new UI. Returns false
    /// when there is no authenticated device on the request (e.g. no user or no device identifier).
    /// </summary>
    Task<bool> UseNewUiAsync();
}
