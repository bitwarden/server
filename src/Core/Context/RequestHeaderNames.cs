namespace Bit.Core.Context;

/// <summary>
/// Names of HTTP request headers that clients send and the server reads.
/// </summary>
public static class RequestHeaderNames
{
    // TODO: Move the other request header names the server reads into this class. They are still string
    // literals at their call sites.

    /// <summary>
    /// Identifier of the requesting device.
    /// </summary>
    public const string DeviceIdentifier = "Device-Identifier";
}
