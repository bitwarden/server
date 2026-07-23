// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.Reflection;
using Bit.Core.Settings;

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
    public const long FileSize25mb = 25L * 1024L * 1024L;
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

    // TODO: PM-34798 Update with actual version once the feature is implemented
    public const string PM32009NewItemTypeMinimumVersion = "2026.2.0";
    public const string DenyLegacyUserMinimumVersion = "2025.6.0";

    /// <summary>
    /// Domain suffixes for Bitwarden cloud-hosted environments.
    /// </summary>
    public static readonly string[] BitwardenCloudDomains =
    [
        // bitwarden.pw is the QA environment domain; not a user-facing cloud region so it
        // has no CloudRegionConfig entry, but must remain in the allowlist for HTTPS redirect
        // validation to pass in QA deployments.
        "bitwarden.pw",
        ..CloudRegionConfig.All.Select(c => c.Domain),
    ];

    /// <summary>
    /// Server permitted SSO callback redirect URIs for mobile clients.
    /// </summary>
    public static readonly string[] BitwardenMobileSsoCallbackUris =
    [
        "bitwarden://sso-callback",
        // bitwarden.pw is the QA environment domain; retained for QA SSO callback validation.
        "https://bitwarden.pw/sso-callback",
        ..CloudRegionConfig.All.Select(c => c.SsoCallbackUri),
    ];

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

        /// <summary>
        /// Abbreviation for Switzerland.
        /// This value must match what Stripe uses for the `Country` field value for Switzerland.
        /// </summary>
        public const string Switzerland = "CH";
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
    public static readonly string NewDeviceVerificationExceptionCacheKeyFormat = "NewDeviceVerificationException_{0}";
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
    public const string ScimInviteUserOptimization = "pm-16811-optimize-invite-user-flow-to-fail-fast";
    public const string AutomaticConfirmUsers = "pm-19934-auto-confirm-organization-users";
    public const string BulkAutoConfirmOnLogin = "pm-35803-browser-auto-confirm-log-in";
    public const string GenerateInviteLink = "pm-32497-generate-invite-link";
    public const string InviteLinkAutoConfirm = "pm-34429-invite-link-auto-confirm";
    public const string PolicyDrawers = "pm-34804-policy-drawers";
    public const string PM35153CollectionSdkDecryption = "pm-35153-collection-sdk-decryption";
    public const string PoliciesInAcceptedState = "pm-34145-policies-in-accepted-state";
    public const string ChangeMemberEmailNoMp = "pm-28365-change-member-email-no-mp";
    public const string PM34423StagedStatus = "pm-34423-staged-status";

    /* Architecture */
    public const string DesktopMigrationMilestone1 = "desktop-ui-migration-milestone-1";
    public const string DesktopMigrationMilestone2 = "desktop-ui-migration-milestone-2";
    public const string DesktopMigrationMilestone3 = "desktop-ui-migration-milestone-3";
    public const string DesktopMigrationMilestone4 = "desktop-ui-migration-milestone-4";
    public const string DesktopMigrationSettings = "desktop-ui-settings-dialog";

    /* Auth Team */
    public const string Otp6Digits = "pm-18612-otp-6-digits";
    public const string PM2035PasskeyUnlock = "pm-2035-passkey-unlock";
    public const string MjmlWelcomeEmailTemplates = "pm-21741-mjml-welcome-email";
    public const string SafariAccountSwitching = "pm-5594-safari-account-switching";
    public const string PM27086_UpdateAuthenticationApisForInputPassword = "pm-27086-update-authentication-apis-for-input-password";
    public const string ChangeEmailNewAuthenticationApis = "pm-30811-change-email-new-authentication-apis";
    public const string PM31088_MasterPasswordServiceEmitSalt = "pm-31088-master-password-service-emit-salt";
    public const string PM32413_MultiClientPasswordManagement = "pm-32413-multi-client-password-management";
    public const string DevicesLastActivityDate = "pm-4516-devices-add-last-activity-date";
    public const string PM34210_DesktopAddDevices = "pm-34210-desktop-add-devices";
    public const string PM37165_RotateUserApiKeyCommand = "pm-37165-rotate-user-api-key-command";
    public const string PM30806_SelfServiceChangeEmailCommand = "pm-30806-self-service-change-email-command";
    public const string PM35092AuthSalesAssistedTrials = "pm-35092-auth-sales-assisted-trials";
    public const string PM27060_PasswordPreloginFromSdk = "pm-27060-password-prelogin-from-sdk";

    /* Autofill Team */
    public const string NotificationRefresh = "notification-refresh";
    public const string FillAssistTargetingRules = "fill-assist-targeting-rules";
    public const string NotificationUndeterminedCipherScenarioLogic = "undetermined-cipher-scenario-logic";
    public const string EnableAutofillTriage = "enable-autofill-triage";
    public const string PM39071_DefaultPasswordManagerPrompt = "pm-39071-default-password-manager-prompt";

    /* Desktop Native Team */
    public const string SSHAgentV2 = "ssh-agent-v2";
    public const string SSHecdsa = "ssh-ecdsa";
    public const string SSHVersionCheckQAOverride = "ssh-version-check-qa-override";
    public const string WindowsDesktopAutotype = "windows-desktop-autotype";
    public const string WindowsDesktopAutotypeGA = "windows-desktop-autotype-ga";
    public const string MacOsNativeCredentialSync = "macos-native-credential-sync";

    /* Billing Team */
    public const string PM23713_PremiumBadgeOpensNewPremiumUpgradeDialog = "pm-23713-premium-badge-opens-new-premium-upgrade-dialog";
    public const string PM29108_EnablePersonalDiscounts = "pm-29108-enable-personal-discounts";
    public const string PM29593_PremiumToOrganizationUpgrade = "pm-29593-premium-to-organization-upgrade";
    public const string PM32581_UseUpdateOrganizationSubscriptionCommand = "pm-32581-use-update-organization-subscription-command";
    public const string PM32645_DeferPriceMigrationToRenewal = "pm-32645-defer-price-migration-to-renewal";
    public const string PM34515_BrowserDesktopCheckout = "pm-34515-browser-desktop-checkout";
    public const string DebugDisableSelfHostPremiumCheck = "debug-disable-self-host-premium-check";
    public const string PM35215_BusinessPlanPriceMigration = "pm-35215-business-plan-price-migration";

    /* Key Management Team */
    public const string PrivateKeyRegeneration = "pm-12241-private-key-regeneration";
    public const string Argon2Default = "argon2-default";
    public const string BiometricsSdkIpc = "biometrics-sdk-ipc";
    public const string SharedUnlockPart1 = "innovation-sprint-shared-unlock-part-1";
    public const string SharedUnlockPart2 = "innovation-sprint-shared-unlock-part-2";
    public const string EnrollAeadOnKeyRotation = "enroll-aead-on-key-rotation";
    public const string ForceUpdateKDFSettings = "pm-18021-force-update-kdf-settings";
    public const string UnlockWithMasterPasswordUnlockData = "pm-23246-unlock-with-master-password-unlock-data";
    public const string LinuxBiometricsV2 = "pm-26340-linux-biometrics-v2";
    public const string NoLogoutOnKdfChange = "pm-23995-no-logout-on-kdf-change";
    public const string V2RegistrationTDEJIT = "pm-27279-v2-registration-tde-jit";
    public const string EnableAccountEncryptionV2KeyConnectorRegistration = "enable-account-encryption-v2-key-connector-registration";
    public const string SdkKeyRotation = "pm-30144-sdk-key-rotation";
    public const string UnlockViaSdk = "unlock-via-sdk";
    public const string UseSdkForKeyConnectorMigration = "use-sdk-for-key-connector-migration";
    public const string UseUnlockServiceForPasswordLogin = "use-unlock-service-for-password-login";
    public const string UseUnlockServiceForKeyConnectorLogin = "use-unlock-service-for-key-connector-login";
    public const string NoLogoutOnKeyUpgradeRotation = "pm-31050-no-logout-key-upgrade-rotation";
    public const string EnableAccountEncryptionV2JitPasswordRegistration = "enable-account-encryption-v2-jit-password-registration";
    public const string EnableAccountEncryptionV2PasswordRegistration = "pm-27278-v2-password-registration";

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
    public const string CxpImportMobile = "cxp-import-mobile";
    public const string CxpExportMobile = "cxp-export-mobile";
    public const string DeviceAuthKey = "pm-27581-device-auth-key";
    public const string PremiumUpgradePath = "pm-31697-premium-upgrade-path";
    public const string MobileCardScanner = "pm-34171-card-scanner";

    /* Platform Team */
    public const string WebPush = "web-push";
    public const string ContentScriptIpcFramework = "content-script-ipc-channel-framework";
    public const string WebAuthnRelatedOrigins = "pm-30529-webauthn-related-origins";
    public const string ElectronStorageCache = "pm-32783-electron-storage-cache";
    public const string AttachmentUploadProgress = "pm-34410-attachment-upload-progress";
    public const string OrgCipherPushFanout = "pm-35168-org-cipher-push-fanout";
    public const string FedRampGovRegion = "fedramp-gov-region";

    /* Tools Team */
    /// <summary>
    /// Enable this flag to share the send view used by the web and browser clients
    /// on the desktop client.
    /// </summary>
    public const string UseSdkPasswordGenerators = "pm-19976-use-sdk-password-generators";
    public const string SendEmailOTP = "pm-19051-send-email-verification";
    public const string SendControls = "pm-31885-send-controls";
    public const string SdkSendsApi = "pm-30110-sdk-sends-api";
    public const string SendEventLogging = "pm-36560-send-event-logging";
    public const string SendControlsExistingSends = "pm-31885-send-controls-existing-sends";
    public const string TemporaryItemSharing = "pm-34203-temporary-item-sharing";

    /* Vault Team */
    public const string CipherKeyEncryption = "cipher-key-encryption";
    public const string PM19941MigrateCipherDomainToSdk = "pm-19941-migrate-cipher-domain-to-sdk";

    public const string PM28190CipherSharingOpsToSdk = "pm-28190-cipher-sharing-ops-to-sdk";
    public const string PhishingDetection = "phishing-detection";
    public const string PM22134SdkCipherListView = "pm-22134-sdk-cipher-list-view";
    public const string PM22136_SdkCipherEncryption = "pm-22136-sdk-cipher-encryption";
    public const string VaultLoadingSkeletons = "pm-25081-vault-skeleton-loaders";
    public const string MigrateMyVaultToMyItems = "pm-20558-migrate-myvault-to-myitems";
    public const string PM27632_CipherCrudOperationsToSdk = "pm-27632-cipher-crud-operations-to-sdk";
    public const string PM28191_CipherAdminOpsToSdk = "pm-28191-cipher-admin-ops-to-sdk";
    public const string PM30521_AutofillButtonViewLoginScreen = "pm-30521-autofill-button-view-login-screen";
    public const string PM32180_PremiumUpsellAccountAge = "pm-32180-premium-upsell-account-age";
    public const string PM29438_WelcomeDialogWithExtensionPrompt = "pm-29438-welcome-dialog-with-extension-prompt";
    public const string PM29438_DialogWithExtensionPromptAccountAge = "pm-29438-dialog-with-extension-prompt-account-age";
    public const string PM31039_ItemActionInExtension = "pm-31039-item-action-in-extension";
    public const string PM29437_WelcomeDialogNoExtPrompt = "pm-29437-welcome-dialog-no-ext-prompt";
    public const string PM31948_OrgUserNotificationBanner = "pm-31948-org-user-notification-banner";
    public const string PM32009_NewItemTypes = "pm-32009-new-item-types";
    public const string PM34500_StrictCipherDecryption = "pm-34500-strict-cipher-decryption";
    public const string PM28091_AddCopyAndQuickLaunchActions = "pm-28091-add-copy-and-quick-launch-actions";
    public const string PM28192_CipherAttachmentOps = "pm-28192-cipher-attachment-ops-to-sdk";
    public const string PM32016_RemoveAtRiskCallout = "pm-32016-remove-at-risk-callout";
    public const string PM37785_VaultBatchBar = "pm-37785-vault-batch-bar";
    public const string PM37785_DesktopVaultBatchBar = "pm-37785-desktop-vault-batch-bar";
    public const string PM29968_FillAfterSave = "pm-29968-fill-after-save";
    public const string PM32380_BtnTextAddCreate = "pm-32380-btn-text-add-create";
    public const string PM40201_DeriveSSHKeys = "pm-40201-derive-ssh-keys";

    /* Secrets Manager Team */
    public const string SecretsVersioning = "sm-1587-secrets-versioning";

    /* Innovation Team */

    /* DIRT Team */
    public const string AccessIntelligenceVersion2 = "pm-31920-access-intelligence-azure-file-storage";
    public const string EventManagementForBlumira = "event-management-for-blumira";
    public const string EventManagementForDataDogAndCrowdStrike = "event-management-for-datadog-and-crowdstrike";
    public const string EventDiagnosticLogging = "pm-27666-siem-event-log-debugging";
    public const string EventManagementForHuntress = "event-management-for-huntress";
    public const string EventManagementForSplunk = "event-management-for-splunk";
    public const string Milestone11AppPageImprovements = "pm-30538-dirt-milestone-11-app-page-improvements";
    public const string AccessIntelligenceNewArchitecture = "pm-31936-access-intelligence-new-architecture";
    public const string PasskeyDirectoryReport = "inno-passkey-directory-report";
    public const string AccessIntelligenceAdoptionUxImprovements = "pm-34723-access-intelligence-adoption-ux-improvements";
    public const string EventManagementForGenericHec = "event-management-for-generic-hec";

    /* UIF Team */
    public const string RouterFocusManagement = "router-focus-management";


    /* PAM */
    public const string Pam = "pm-37044-pam-v-0";

    /* VFO */
    public const string VFO1Foundation = "vfo1-foundation";

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
