namespace Bit.Core.Auth.Enums;

/// <summary>
/// Deeplink scheme values used for mobile client redirects after Duo authentication.
/// </summary>
public enum DuoDeeplinkScheme : byte
{
    /// <summary>
    /// HTTPS scheme used for Bitwarden cloud-hosted environments.
    /// </summary>
    Https = 0,

    /// <summary>
    /// Custom bitwarden:// scheme used for self-hosted environments.
    /// </summary>
    Bitwarden = 1,
}
