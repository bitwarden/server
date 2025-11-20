// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.Reflection;

namespace Bit.Core;

public static class Constants
{
    public const int BypassFiltersEventId = 12482444;
    public const int FailedSecretVerificationDelay = 2000;

    /// <summary>
    /// Self-hosted max storage limit in GB (10 TB).
    /// </summary>
    public const short SelfHostedMaxStorageGb = 10240;

    // File size limits - give 1 MB extra for cushion.
    // Note: if request size limits are changed, 'client_max_body_size'
    // in nginx/proxy.conf may also need to be updated accordingly.
    public const long FileSize101mb = 101L * 1024L * 1024L;
    public const long FileSize501mb = 501L * 1024L * 1024L;
    public const string DatabaseFieldProtectorPurpose = "DatabaseFieldProtection";
    public const string DatabaseFieldProtectedPrefix = "P|";

    /// <summary>
    /// Default number of days an organization has to apply an updated license to their self-hosted installation after
    /// their subscription has expired.
    /// </summary>
    public const int OrganizationSelfHostSubscriptionGracePeriodDays = 60;

    public const string Fido2KeyCipherMinimumVersion = "2023.10.0";
    public const string SSHKeyCipherMinimumVersion = "2024.12.0";
    public const string DenyLegacyUserMinimumVersion = "2025.6.0";

    /// <summary>
    /// Used by IdentityServer to identify our own provider.
    /// </summary>
    public const string IdentityProvider = "bitwarden";

    /// <summary>
    /// Date identifier used in ProviderService to determine if a provider was created before Nov 6, 2023.
    /// If true, the organization plan assigned to that provider is updated to a 2020 plan.
    /// </summary>
    public static readonly DateTime ProviderCreatedPriorNov62023 = new DateTime(2023, 11, 6);

    /// <summary>
    /// When you set the ProrationBehavior to create_prorations,
    /// Stripe will automatically create prorations for any changes made to the subscription,
    /// such as changing the plan, adding or removing quantities, or applying discounts.
    /// </summary>
    public const string CreateProrations = "create_prorations";

    /// <summary>
    /// When you set the ProrationBehavior to always_invoice,
    /// Stripe will always generate an invoice when a subscription update occurs,
    /// regardless of whether there is a proration or not.
    /// </summary>
    public const string AlwaysInvoice = "always_invoice";

    /// <summary>
    /// Used primarily to determine whether a customer's business is inside or outside the United States
    /// for billing purposes.
    /// </summary>
    public static class CountryAbbreviations
    {
        /// <summary>
        /// Abbreviation for The United States.
        /// This value must match what Stripe uses for the `Country` field value for the United States.
        /// </summary>
        public const string UnitedStates = "US";
    }


    /// <summary>
    /// Constants for our browser extensions IDs
    /// </summary>
    public static class BrowserExtensions
    {
        public const string ChromeId = "chrome-extension://nngceckbapebfimnlniiiahkandclblb/";
        public const string EdgeId = "chrome-extension://jbkfoedolllekgbhcbcoahefnbanhhlh/";
        public const string OperaId = "chrome-extension://ccnckbpmaceehanjmeomladnmlffdjgn/";
    }
}

public static class AuthConstants
{
    public static readonly RangeConstant PBKDF2_ITERATIONS = new(600_000, 2_000_000, 600_000);

    public static readonly RangeConstant ARGON2_ITERATIONS = new(2, 10, 3);
    public static readonly RangeConstant ARGON2_MEMORY = new(15, 1024, 64);
    public static readonly RangeConstant ARGON2_PARALLELISM = new(1, 16, 4);
    public static readonly string NewDeviceVerificationExceptionCacheKeyFormat = "NewDeviceVerificationException_{0}";
}

public class RangeConstant
{
    public int Default { get; }
    public int Min { get; }
    public int Max { get; }

    public RangeConstant(int min, int max, int defaultValue)
    {
        Default = defaultValue;
        Min = min;
        Max = max;

        if (Min > Max)
        {
            throw new ArgumentOutOfRangeException($"{Min} is larger than {Max}.");
        }

        if (!InsideRange(defaultValue))
        {
            throw new ArgumentOutOfRangeException($"{Default} is outside allowed range of {Min}-{Max}.");
        }
    }

    public bool InsideRange(int number)
    {
        return Min <= number && number <= Max;
    }
}

public static class TokenPurposes
{
    public const string LinkSso = "LinkSso";
}

public static class AuthenticationSchemes
{
    public const string BitwardenExternalCookieAuthenticationScheme = "bw.external";
}

public static class FeatureFlagKeys
{
    /* Admin Console Team */
    public const string PolicyRequirements = "pm-14439-policy-requirements";
    public const string ScimInviteUserOptimization = "pm-16811-optimize-invite-user-flow-to-fail-fast";
    public const string CreateDefaultLocation = "pm-19467-create-default-location";
    public const string AutomaticConfirmUsers = "pm-19934-auto-confirm-organization-users";
    public const string PM23845_VNextApplicationCache = "pm-24957-refactor-memory-application-cache";
    public const string AccountRecoveryCommand = "pm-25581-prevent-provider-account-recovery";
    public const string BlockClaimedDomainAccountCreation = "pm-28297-block-uninvited-claimed-domain-registration";
    public const string PolicyValidatorsRefactor = "pm-26423-refactor-policy-side-effects";

    /* Architecture */
    public const string DesktopMigrationMilestone1 = "desktop-ui-migration-milestone-1";
    public const string DesktopMigrationMilestone2 = "desktop-ui-migration-milestone-2";
    public const string DesktopMigrationMilestone3 = "desktop-ui-migration-milestone-3";

    /* Auth Team */
    public const string TwoFactorExtensionDataPersistence = "pm-9115-two-factor-extension-data-persistence";
    public const string EmailVerification = "email-verification";
    public const string BrowserExtensionLoginApproval = "pm-14938-browser-extension-login-approvals";
    public const string SetInitialPasswordRefactor = "pm-16117-set-initial-password-refactor";
    public const string ChangeExistingPasswordRefactor = "pm-16117-change-existing-password-refactor";
    public const string Otp6Digits = "pm-18612-otp-6-digits";
    public const string FailedTwoFactorEmail = "pm-24425-send-2fa-failed-email";
    public const string PM24579_PreventSsoOnExistingNonCompliantUsers = "pm-24579-prevent-sso-on-existing-non-compliant-users";
    public const string DisableAlternateLoginMethods = "pm-22110-disable-alternate-login-methods";
    public const string PM23174ManageAccountRecoveryPermissionDrivesTheNeedToSetMasterPassword =
        "pm-23174-manage-account-recovery-permission-drives-the-need-to-set-master-password";
    public const string RecoveryCodeSupportForSsoRequiredUsers = "pm-21153-recovery-code-support-for-sso-required";
    public const string MJMLBasedEmailTemplates = "mjml-based-email-templates";
    public const string MjmlWelcomeEmailTemplates = "pm-21741-mjml-welcome-email";
    public const string MarketingInitiatedPremiumFlow = "pm-26140-marketing-initiated-premium-flow";

    /* Autofill Team */
    public const string IdpAutoSubmitLogin = "idp-auto-submit-login";
    public const string UseTreeWalkerApiForPageDetailsCollection = "use-tree-walker-api-for-page-details-collection";
    public const string InlineMenuFieldQualification = "inline-menu-field-qualification";
    public const string InlineMenuPositioningImprovements = "inline-menu-positioning-improvements";
    public const string SSHAgent = "ssh-agent";
    public const string SSHAgentV2 = "ssh-agent-v2";
    public const string SSHVersionCheckQAOverride = "ssh-version-check-qa-override";
    public const string GenerateIdentityFillScriptRefactor = "generate-identity-fill-script-refactor";
    public const string DelayFido2PageScriptInitWithinMv2 = "delay-fido2-page-script-init-within-mv2";
    public const string NotificationBarAddLoginImprovements = "notification-bar-add-login-improvements";
    public const string BlockBrowserInjectionsByDomain = "block-browser-injections-by-domain";
    public const string NotificationRefresh = "notification-refresh";
    public const string EnableNewCardCombinedExpiryAutofill = "enable-new-card-combined-expiry-autofill";
    public const string MacOsNativeCredentialSync = "macos-native-credential-sync";
    public const string InlineMenuTotp = "inline-menu-totp";
    public const string WindowsDesktopAutotype = "windows-desktop-autotype";

    /* Billing Team */
    public const string TrialPayment = "PM-8163-trial-payment";
    public const string PM19422_AllowAutomaticTaxUpdates = "pm-19422-allow-automatic-tax-updates";
    public const string PM21821_ProviderPortalTakeover = "pm-21821-provider-portal-takeover";
    public const string PM22415_TaxIDWarnings = "pm-22415-tax-id-warnings";
    public const string PM25379_UseNewOrganizationMetadataStructure = "pm-25379-use-new-organization-metadata-structure";
    public const string PM24996ImplementUpgradeFromFreeDialog = "pm-24996-implement-upgrade-from-free-dialog";
    public const string PM24032_NewNavigationPremiumUpgradeButton = "pm-24032-new-navigation-premium-upgrade-button";
    public const string PM23713_PremiumBadgeOpensNewPremiumUpgradeDialog = "pm-23713-premium-badge-opens-new-premium-upgrade-dialog";
    public const string PremiumUpgradeNewDesign = "pm-24033-updat-premium-subscription-page";
    public const string PM26793_FetchPremiumPriceFromPricingService = "pm-26793-fetch-premium-price-from-pricing-service";
    public const string PM23341_Milestone_2 = "pm-23341-milestone-2";
    public const string PM26462_Milestone_3 = "pm-26462-milestone-3";

    /* Key Management Team */
    public const string ReturnErrorOnExistingKeypair = "return-error-on-existing-keypair";
    public const string PM4154BulkEncryptionService = "PM-4154-bulk-encryption-service";
    public const string PrivateKeyRegeneration = "pm-12241-private-key-regeneration";
    public const string Argon2Default = "argon2-default";
    public const string UserkeyRotationV2 = "userkey-rotation-v2";
    public const string SSHKeyItemVaultItem = "ssh-key-vault-item";
    public const string UserSdkForDecryption = "use-sdk-for-decryption";
    public const string EnrollAeadOnKeyRotation = "enroll-aead-on-key-rotation";
    public const string PM17987_BlockType0 = "pm-17987-block-type-0";
    public const string ForceUpdateKDFSettings = "pm-18021-force-update-kdf-settings";
    public const string UnlockWithMasterPasswordUnlockData = "pm-23246-unlock-with-master-password-unlock-data";
    public const string WindowsBiometricsV2 = "pm-25373-windows-biometrics-v2";
    public const string LinuxBiometricsV2 = "pm-26340-linux-biometrics-v2";
    public const string NoLogoutOnKdfChange = "pm-23995-no-logout-on-kdf-change";
    public const string DisableType0Decryption = "pm-25174-disable-type-0-decryption";
    public const string ConsolidatedSessionTimeoutComponent = "pm-26056-consolidated-session-timeout-component";

    /* Mobile Team */
    public const string AndroidImportLoginsFlow = "import-logins-flow";
    public const string AndroidMutualTls = "mutual-tls";
    public const string SingleTapPasskeyCreation = "single-tap-passkey-creation";
    public const string SingleTapPasskeyAuthentication = "single-tap-passkey-authentication";
    public const string PM3503_MobileAnonAddySelfHostAlias = "anon-addy-self-host-alias";
    public const string PM3553_MobileSimpleLoginSelfHostAlias = "simple-login-self-host-alias";
    public const string MobileErrorReporting = "mobile-error-reporting";
    public const string AndroidChromeAutofill = "android-chrome-autofill";
    public const string UserManagedPrivilegedApps = "pm-18970-user-managed-privileged-apps";
    public const string SendAccess = "pm-19394-send-access-control";
    public const string CxpImportMobile = "cxp-import-mobile";
    public const string CxpExportMobile = "cxp-export-mobile";

    /* Platform Team */
    public const string IpcChannelFramework = "ipc-channel-framework";
    public const string PushNotificationsWhenLocked = "pm-19388-push-notifications-when-locked";
    public const string PushNotificationsWhenInactive = "pm-25130-receive-push-notifications-for-inactive-users";

    /* Tools Team */
    public const string DesktopSendUIRefresh = "desktop-send-ui-refresh";
    public const string UseSdkPasswordGenerators = "pm-19976-use-sdk-password-generators";
    public const string UseChromiumImporter = "pm-23982-chromium-importer";
    public const string ChromiumImporterWithABE = "pm-25855-chromium-importer-abe";

    /* Vault Team */
    public const string PM8851_BrowserOnboardingNudge = "pm-8851-browser-onboarding-nudge";
    public const string CipherKeyEncryption = "cipher-key-encryption";
    public const string PM19941MigrateCipherDomainToSdk = "pm-19941-migrate-cipher-domain-to-sdk";
    public const string EndUserNotifications = "pm-10609-end-user-notifications";
    public const string PhishingDetection = "phishing-detection";
    public const string RemoveCardItemTypePolicy = "pm-16442-remove-card-item-type-policy";
    public const string PM22134SdkCipherListView = "pm-22134-sdk-cipher-list-view";
    public const string PM19315EndUserActivationMvp = "pm-19315-end-user-activation-mvp";
    public const string PM22136_SdkCipherEncryption = "pm-22136-sdk-cipher-encryption";
    public const string PM23904_RiskInsightsForPremium = "pm-23904-risk-insights-for-premium";
    public const string PM25083_AutofillConfirmFromSearch = "pm-25083-autofill-confirm-from-search";
    public const string VaultLoadingSkeletons = "pm-25081-vault-skeleton-loaders";

    /* Innovation Team */
    public const string ArchiveVaultItems = "pm-19148-innovation-archive";

    /* DIRT Team */
    public const string PM22887_RiskInsightsActivityTab = "pm-22887-risk-insights-activity-tab";
    public const string EventManagementForDataDogAndCrowdStrike = "event-management-for-datadog-and-crowdstrike";
    public const string EventDiagnosticLogging = "pm-27666-siem-event-log-debugging";

    public static List<string> GetAllKeys()
    {
        return typeof(FeatureFlagKeys).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(fi => fi.IsLiteral && !fi.IsInitOnly && fi.FieldType == typeof(string))
            .Select(x => (string)x.GetRawConstantValue())
            .ToList();
    }

    public static Dictionary<string, string> GetLocalOverrideFlagValues()
    {
        // place overriding values when needed locally (offline), or return null
        return null;
    }
}
