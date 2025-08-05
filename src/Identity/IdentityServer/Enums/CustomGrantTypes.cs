namespace Bit.Identity.IdentityServer.Enums;

/// <summary>
/// A class containing custom grant types used in the Bitwarden IdentityServer implementation
/// </summary>
public static class CustomGrantTypes
{
    public const string SendAccess = "send_access";
    // TODO: PM-24471 replace magic string with a constant for webauthn
    public const string WebAuthn = "webauthn";
}