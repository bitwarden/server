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
    /// <summary>
    /// Whether the calling client can be sent partial ciphers: the web vault, and a browser extension
    /// while <see cref="FeatureFlagKeys.PamBrowserPartialCiphers"/> is on.
    /// </summary>
    /// <remarks>
    /// Fails safe: an absent or unrecognized device type maps to <see cref="ClientType.All"/>, which is
    /// not supported, so an unknown caller is treated as unable to handle the shape.
    /// TODO: restore the <see cref="Constants.PamPartialCipherMinimumVersion"/> check on
    /// <paramref name="clientVersion"/>; it is temporarily disabled.
    /// </remarks>
    public static bool IsSupportedBy(DeviceType? deviceType, Version? clientVersion, bool browserExtensionsEnabled) =>
        DeviceTypes.ToClientType(deviceType) switch
        {
            ClientType.Web => true,
            ClientType.Browser => browserExtensionsEnabled,
            _ => false,
        };
}
