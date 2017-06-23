namespace Bit.Core.Enums
{
    public enum TwoFactorProviderType : byte
    {
        Authenticator = 0,
        Email = 1,
        Duo = 2,
        YubiKey = 3,
        U2f = 4,
        Remember = 5
    }
}
