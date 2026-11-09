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
/// item is the lesser harm — the user sees it in the web vault, where they can request access.
/// </remarks>
public static class PartialCipherSupport
{
    /// <summary>
    /// Whether the calling client can be sent partial ciphers: the web vault always, a browser extension only
    /// while <see cref="FeatureFlagKeys.PamBrowserPartialCiphers"/> is on.
    /// </summary>
    /// <remarks>
    /// Fails safe: an absent or unrecognized device type maps to <see cref="ClientType.All"/>, which is
    /// not supported, so an unknown caller is treated as unable to handle the shape.
    /// The web vault is served from the same deployment as the server, so there is no version skew to
    /// account for. Browser extensions do skew, and the flag stands in for their minimum-version check.
    /// </remarks>
    public static bool IsSupportedBy(DeviceType? deviceType, bool browserExtensionsEnabled) =>
        DeviceTypes.ToClientType(deviceType) switch
        {
            ClientType.Web => true,
            ClientType.Browser => browserExtensionsEnabled,
            _ => false,
        };
}
