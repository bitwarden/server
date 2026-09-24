using Bit.Core.Enums;
using Bit.Core.Utilities;

namespace Bit.Core.Vault.Authorization;

/// <summary>
/// Which clients understand the reduced partial-data cipher shape emitted for leasing-gated ciphers
/// (see <see cref="Models.Data.PartialCipherData"/>).
/// </summary>
/// <remarks>
/// A client that does not understand the shape must have gated ciphers omitted entirely: it would show the
/// item as empty and could save blanks over the withheld fields.
/// </remarks>
public static class PartialCipherSupport
{
    /// <summary>
    /// Whether the calling client can be sent partial ciphers: the web vault always, a browser extension
    /// only while <see cref="FeatureFlagKeys.PamBrowserPartialCiphers"/> is on.
    /// </summary>
    /// <remarks>An absent or unrecognized device type maps to <see cref="ClientType.All"/> and is not supported.</remarks>
    public static bool IsSupportedBy(DeviceType? deviceType, bool browserExtensionsEnabled) =>
        DeviceTypes.ToClientType(deviceType) switch
        {
            ClientType.Web => true,
            ClientType.Browser => browserExtensionsEnabled,
            _ => false,
        };
}
