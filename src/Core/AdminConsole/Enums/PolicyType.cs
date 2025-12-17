namespace Bit.Core.AdminConsole.Enums;

public enum PolicyType : byte
{
    TwoFactorAuthentication = 0,
    MasterPassword = 1,
    PasswordGenerator = 2,
    SingleOrg = 3,
    RequireSso = 4,
    OrganizationDataOwnership = 5,
    DisableSend = 6,
    SendOptions = 7,
    ResetPassword = 8,
    MaximumVaultTimeout = 9,
    DisablePersonalVaultExport = 10,
    ActivateAutofill = 11,
    AutomaticAppLogIn = 12,
    FreeFamiliesSponsorshipPolicy = 13,
    RemoveUnlockWithPin = 14,
    RestrictedItemTypesPolicy = 15,
    UriMatchDefaults = 16,
    AutotypeDefaultSetting = 17,
    AutomaticUserConfirmation = 18,
    BlockClaimedDomainAccountCreation = 19,
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
            PolicyType.OrganizationDataOwnership => "Enforce organization data ownership",
            PolicyType.DisableSend => "Remove Send",
            PolicyType.SendOptions => "Send options",
            PolicyType.ResetPassword => "Account recovery administration",
            PolicyType.MaximumVaultTimeout => "Vault timeout",
            PolicyType.DisablePersonalVaultExport => "Remove individual vault export",
            PolicyType.ActivateAutofill => "Active auto-fill",
            PolicyType.AutomaticAppLogIn => "Automatic login with SSO",
            PolicyType.FreeFamiliesSponsorshipPolicy => "Remove Free Bitwarden Families sponsorship",
            PolicyType.RemoveUnlockWithPin => "Remove unlock with PIN",
            PolicyType.RestrictedItemTypesPolicy => "Restricted item types",
            PolicyType.UriMatchDefaults => "URI match defaults",
            PolicyType.AutotypeDefaultSetting => "Autotype default setting",
            PolicyType.AutomaticUserConfirmation => "Automatically confirm invited users",
            PolicyType.BlockClaimedDomainAccountCreation => "Block account creation for claimed domains",
        };
    }
}
