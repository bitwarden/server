using Bit.Core.Enums;
using Bit.Core.Utilities;

namespace Bit.Core.Vault.Authorization;

/// <summary>
/// Which clients understand the reduced partial-data cipher shape emitted for leasing-gated ciphers
/// (see <see cref="Models.Data.PartialCipherData"/>).
/// </summary>
/// <remarks>
/// A client that does not understand the shape must have gated ciphers <em>omitted entirely</em> rather
/// than be sent a partial one: it would render an item with no credentials as though it were empty, and
/// saving it back would overwrite the withheld fields with the blanks the client holds. Dropping the
/// item is the lesser harm — it reappears once the client is updated.
/// </remarks>
public static class PartialCipherSupport
{
    private static readonly Version _minimumVersion = new(Constants.PamPartialCipherMinimumVersion);

    /// <summary>
    /// Whether the calling client can be sent partial ciphers: the web vault at
    /// <see cref="Constants.PamPartialCipherMinimumVersion"/> or later, and a browser extension at that version
    /// or later while <see cref="FeatureFlagKeys.PamBrowserPartialCiphers"/> is on.
    /// </summary>
    /// <remarks>
    /// Fails safe: an absent or unrecognized device type maps to <see cref="ClientType.All"/>, which is
    /// not supported, and a client that sends no version is treated as too old, so an unknown caller is
    /// treated as unable to handle the shape. The version check covers web vault tabs left open across a
    /// deploy as well as extension skew; the flag is an extra kill switch for extensions only.
    /// </remarks>
    public static bool IsSupportedBy(DeviceType? deviceType, Version? clientVersion, bool browserExtensionsEnabled) =>
        DeviceTypes.ToClientType(deviceType) switch
        {
            ClientType.Web => MeetsMinimumVersion(clientVersion),
            ClientType.Browser => browserExtensionsEnabled && MeetsMinimumVersion(clientVersion),
            _ => false,
        };

    private static bool MeetsMinimumVersion(Version? clientVersion) =>
        clientVersion is not null && clientVersion >= _minimumVersion;
}
