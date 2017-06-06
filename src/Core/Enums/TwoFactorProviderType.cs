namespace Bit.Core.Enums
{
    public enum TwoFactorProviderType : byte
    {
        Authenticator = 0,
        Email = 1,
        Duo = 2,
        YubiKey = 3
    }
}
