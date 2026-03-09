#nullable enable

using System.Net;
using CoreIPAddressExtensions = Bit.Core.Utilities.IPAddressExtensions;

namespace Bit.Icons.Extensions;

/// <summary>
/// Delegates to <see cref="CoreIPAddressExtensions"/> for shared SSRF protection logic.
/// Maintained for backward compatibility within the Icons project.
/// </summary>
public static class IPAddressExtension
{
    public static bool IsInternal(this IPAddress ip) => CoreIPAddressExtensions.IsInternal(ip);
}
