namespace Bit.Core.Enums
{
    public enum PolicyType : byte
    {
        TwoFactorAuthentication = 0,
        MasterPassword = 1,
        PasswordGenerator = 2,
        SingleOrg = 3,
        RequireSso = 4,
        PersonalOwnership = 5,
    }
}
