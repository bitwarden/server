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
    /// Whether the calling client can be sent partial ciphers. Only the web vault can today.
    /// </summary>
    /// <remarks>
    /// Fails safe: an absent or unrecognized device type maps to <see cref="ClientType.All"/>, which is
    /// not <see cref="ClientType.Web"/>, so an unknown caller is treated as unable to handle the shape.
    /// The web vault is served from the same deployment as the server, so there is no version skew to
    /// account for; other clients will need a minimum-version check when they gain support.
    /// </remarks>
    public static bool IsSupportedBy(DeviceType? deviceType) =>
        DeviceTypes.ToClientType(deviceType) == ClientType.Web;
}
