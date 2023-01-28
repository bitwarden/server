namespace Bit.Core.Enums;

public enum PolicyType : byte
{
    TwoFactorAuthentication = 0,
    MasterPassword = 1,
    PasswordGenerator = 2,
    SingleOrg = 3,
    RequireSso = 4,
    PersonalOwnership = 5,
    DisableSend = 6,
    SendOptions = 7,
    ResetPassword = 8,
    MaximumVaultTimeout = 9,
    DisablePersonalVaultExport = 10,
}
