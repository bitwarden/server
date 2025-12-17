using System.Text.Json.Serialization;

namespace Bit.Core.Tools.Enums;

/// <summary>
/// Specifies the authentication method required to access a Send.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AuthType : byte
{
    /// <summary>
    /// Email-based OTP authentication
    /// </summary>
    Email = 0,

    /// <summary>
    /// Password-based authentication
    /// </summary>
    Password = 1,

    /// <summary>
    /// No authentication required
    /// </summary>
    None = 2
}
