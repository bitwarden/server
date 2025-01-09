namespace Bit.Core.AdminConsole.Enums;

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
    ActivateAutofill = 11,
    AutomaticAppLogIn = 12,
    FreeFamiliesSponsorshipPolicy = 13
}

public static class PolicyTypeExtensions
{
    /// <summary>
    /// Returns the name of the policy for display to the user.
    /// Do not include the word "policy" in the return value.
    /// </summary>
    public static string GetName(this PolicyType type)
    {
        return type switch
        {
            PolicyType.TwoFactorAuthentication => "Require two-step login",
            PolicyType.MasterPassword => "Master password requirements",
            PolicyType.PasswordGenerator => "Password generator",
            PolicyType.SingleOrg => "Single organization",
            PolicyType.RequireSso => "Require single sign-on authentication",
            PolicyType.PersonalOwnership => "Remove individual vault",
            PolicyType.DisableSend => "Remove Send",
            PolicyType.SendOptions => "Send options",
            PolicyType.ResetPassword => "Account recovery administration",
            PolicyType.MaximumVaultTimeout => "Vault timeout",
            PolicyType.DisablePersonalVaultExport => "Remove individual vault export",
            PolicyType.ActivateAutofill => "Active auto-fill",
            PolicyType.AutomaticAppLogIn => "Automatically log in users for allowed applications",
            PolicyType.FreeFamiliesSponsorshipPolicy => "Remove Free Bitwarden Families sponsorship"
        };
    }
}
