namespace Bit.Core.Auth.Enums;

public enum TwoFactorProviderType : byte
{
    Authenticator = 0,
    Email = 1,
    Duo = 2,
    YubiKey = 3,
    [Obsolete("Deprecated in favor of WebAuthn.")]
    U2f = 4,
    Remember = 5,
    OrganizationDuo = 6,
    WebAuthn = 7,
    RecoveryCode = 8,
}
