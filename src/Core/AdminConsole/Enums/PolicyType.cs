namespace Bit.Core.AdminConsole.Enums;

public enum PolicyType : byte
{
    TwoFactorAuthentication = 0,
    MasterPassword = 1,
    PasswordGenerator = 2,
    SingleOrg = 3,
    RequireSso = 4,
    OrganizationDataOwnership = 5,
    // Deprecated: superseded by SendControls (21) when pm-31885-send-controls flag is active.
    // Do not add [Obsolete] until the flag is retired.
    DisableSend = 6,
    // Deprecated: superseded by SendControls (21) when pm-31885-send-controls flag is active.
    // Do not add [Obsolete] until the flag is retired.
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
    OrganizationUserNotification = 20,
    /// <summary>
    /// Supersedes DisableSend (6) and SendOptions (7) when the pm-31885-send-controls feature flag is active.
    /// </summary>
    SendControls = 21,
    FillAssist = 22,
}

public static class PolicyTypeExtensions
{
    /// <summary>
    /// Returns the name of the policy for display to the user.
    /// Do not include the word "policy" in the return value.
    /// Keep these in sync with the policy titles shown in the web client's Admin Console.
    /// </summary>
    /// <param name="type">The policy type.</param>
    /// <param name="useVfo1Terminology">
    /// Whether the <see cref="FeatureFlagKeys.VFO1Foundation"/> feature flag is enabled. Some policies are
    /// displayed under different names when it is.
    /// </param>
    public static string GetName(this PolicyType type, bool useVfo1Terminology)
    {
        if (useVfo1Terminology)
        {
            var vfo1Name = type switch
            {
                PolicyType.SingleOrg => "Single organization membership",
                PolicyType.RequireSso => "Require SSO",
                PolicyType.OrganizationDataOwnership => "Centralized organization ownership",
                PolicyType.FreeFamiliesSponsorshipPolicy => "Remove Sponsored Families Plan",
                PolicyType.RemoveUnlockWithPin => "Remove unlock with PIN",
                _ => null,
            };

            if (vfo1Name != null)
            {
                return vfo1Name;
            }
        }

        return type switch
        {
            PolicyType.TwoFactorAuthentication => "Require two-step login",
            PolicyType.MasterPassword => "Master password requirements",
            PolicyType.PasswordGenerator => "Password generator",
            PolicyType.SingleOrg => "Single organization",
            PolicyType.RequireSso => "Require single sign-on (SSO)",
            PolicyType.OrganizationDataOwnership => "Centralize organization ownership",
            PolicyType.DisableSend => "Remove Send",
            PolicyType.SendOptions => "Send options",
            PolicyType.ResetPassword => "Account recovery administration",
            PolicyType.MaximumVaultTimeout => "Session timeout",
            PolicyType.DisablePersonalVaultExport => "Remove export",
            PolicyType.ActivateAutofill => "Enable autofill on page load",
            PolicyType.AutomaticAppLogIn => "Automatic login with SSO",
            PolicyType.FreeFamiliesSponsorshipPolicy => "Remove sponsored Families plan",
            PolicyType.RemoveUnlockWithPin => "Remove Unlock with PIN",
            PolicyType.RestrictedItemTypesPolicy => "Remove card item type",
            PolicyType.UriMatchDefaults => "Default URI match detection",
            PolicyType.AutotypeDefaultSetting => "Desktop autotype default setting",
            PolicyType.AutomaticUserConfirmation => "Automatic user confirmation",
            PolicyType.BlockClaimedDomainAccountCreation => "Block account creation for claimed domains",
            PolicyType.OrganizationUserNotification => "Vault banner",
            PolicyType.SendControls => "Manage Send and share",
            PolicyType.FillAssist => "Activate fill assist",
        };
    }
}
