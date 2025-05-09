using System.Reflection;

namespace Bit.Core;

public static class Constants
{
    public const int BypassFiltersEventId = 12482444;
    public const int FailedSecretVerificationDelay = 2000;

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
    public const string AccountDeprovisioning = "pm-10308-account-deprovisioning";
    public const string VerifiedSsoDomainEndpoint = "pm-12337-refactor-sso-details-endpoint";
    public const string LimitItemDeletion = "pm-15493-restrict-item-deletion-to-can-manage-permission";
    public const string PolicyRequirements = "pm-14439-policy-requirements";
    public const string SsoExternalIdVisibility = "pm-18630-sso-external-id-visibility";
    public const string ScimInviteUserOptimization = "pm-16811-optimize-invite-user-flow-to-fail-fast";
    public const string EventBasedOrganizationIntegrations = "event-based-organization-integrations";

    /* Auth Team */
    public const string PM9112DeviceApprovalPersistence = "pm-9112-device-approval-persistence";
    public const string TwoFactorExtensionDataPersistence = "pm-9115-two-factor-extension-data-persistence";
    public const string EmailVerification = "email-verification";
    public const string UnauthenticatedExtensionUIRefresh = "unauth-ui-refresh";
    public const string NewDeviceVerification = "new-device-verification";
    public const string SetInitialPasswordRefactor = "pm-16117-set-initial-password-refactor";
    public const string ChangeExistingPasswordRefactor = "pm-16117-change-existing-password-refactor";
    public const string RecoveryCodeLogin = "pm-17128-recovery-code-login";

    /* Autofill Team */
    public const string IdpAutoSubmitLogin = "idp-auto-submit-login";
    public const string UseTreeWalkerApiForPageDetailsCollection = "use-tree-walker-api-for-page-details-collection";
    public const string InlineMenuFieldQualification = "inline-menu-field-qualification";
    public const string InlineMenuPositioningImprovements = "inline-menu-positioning-improvements";
    public const string SSHAgent = "ssh-agent";
    public const string SSHVersionCheckQAOverride = "ssh-version-check-qa-override";
    public const string GenerateIdentityFillScriptRefactor = "generate-identity-fill-script-refactor";
    public const string DelayFido2PageScriptInitWithinMv2 = "delay-fido2-page-script-init-within-mv2";
    public const string NotificationBarAddLoginImprovements = "notification-bar-add-login-improvements";
    public const string BlockBrowserInjectionsByDomain = "block-browser-injections-by-domain";
    public const string NotificationRefresh = "notification-refresh";
    public const string EnableNewCardCombinedExpiryAutofill = "enable-new-card-combined-expiry-autofill";
    public const string MacOsNativeCredentialSync = "macos-native-credential-sync";
    public const string InlineMenuTotp = "inline-menu-totp";

    /* Billing Team */
    public const string AC2101UpdateTrialInitiationEmail = "AC-2101-update-trial-initiation-email";
    public const string TrialPayment = "PM-8163-trial-payment";
    public const string PM17772_AdminInitiatedSponsorships = "pm-17772-admin-initiated-sponsorships";
    public const string UsePricingService = "use-pricing-service";
    public const string PM12276Breadcrumbing = "pm-12276-breadcrumbing-for-business-features";
    public const string PM18794_ProviderPaymentMethod = "pm-18794-provider-payment-method";
    public const string PM19147_AutomaticTaxImprovements = "pm-19147-automatic-tax-improvements";
    public const string PM19422_AllowAutomaticTaxUpdates = "pm-19422-allow-automatic-tax-updates";
    public const string PM18770_EnableOrganizationBusinessUnitConversion = "pm-18770-enable-organization-business-unit-conversion";
    public const string PM199566_UpdateMSPToChargeAutomatically = "pm-199566-update-msp-to-charge-automatically";
    public const string PM19956_RequireProviderPaymentMethodDuringSetup = "pm-19956-require-provider-payment-method-during-setup";
    public const string UseOrganizationWarningsService = "use-organization-warnings-service";
    public const string PM20322_AllowTrialLength0 = "pm-20322-allow-trial-length-0";

    /* Data Insights and Reporting Team */
    public const string RiskInsightsCriticalApplication = "pm-14466-risk-insights-critical-application";
    public const string EnableRiskInsightsNotifications = "enable-risk-insights-notifications";

    /* Key Management Team */
    public const string ReturnErrorOnExistingKeypair = "return-error-on-existing-keypair";
    public const string PM4154BulkEncryptionService = "PM-4154-bulk-encryption-service";
    public const string PrivateKeyRegeneration = "pm-12241-private-key-regeneration";
    public const string Argon2Default = "argon2-default";
    public const string UserkeyRotationV2 = "userkey-rotation-v2";
    public const string SSHKeyItemVaultItem = "ssh-key-vault-item";
    public const string UserSdkForDecryption = "use-sdk-for-decryption";
    public const string PM17987_BlockType0 = "pm-17987-block-type-0";

    /* Mobile Team */
    public const string NativeCarouselFlow = "native-carousel-flow";
    public const string NativeCreateAccountFlow = "native-create-account-flow";
    public const string AndroidImportLoginsFlow = "import-logins-flow";
    public const string AppReviewPrompt = "app-review-prompt";
    public const string EnablePasswordManagerSyncAndroid = "enable-password-manager-sync-android";
    public const string EnablePasswordManagerSynciOS = "enable-password-manager-sync-ios";
    public const string AndroidMutualTls = "mutual-tls";
    public const string SingleTapPasskeyCreation = "single-tap-passkey-creation";
    public const string SingleTapPasskeyAuthentication = "single-tap-passkey-authentication";
    public const string EnablePMAuthenticatorSync = "enable-pm-bwa-sync";
    public const string PM3503_MobileAnonAddySelfHostAlias = "anon-addy-self-host-alias";
    public const string PM3553_MobileSimpleLoginSelfHostAlias = "simple-login-self-host-alias";
    public const string EnablePMFlightRecorder = "enable-pm-flight-recorder";
    public const string MobileErrorReporting = "mobile-error-reporting";
    public const string AndroidChromeAutofill = "android-chrome-autofill";

    /* Platform Team */
    public const string PersistPopupView = "persist-popup-view";
    public const string StorageReseedRefactor = "storage-reseed-refactor";
    public const string WebPush = "web-push";
    public const string RecordInstallationLastActivityDate = "installation-last-activity-date";
    public const string IpcChannelFramework = "ipc-channel-framework";

    /* Tools Team */
    public const string ItemShare = "item-share";
    public const string DesktopSendUIRefresh = "desktop-send-ui-refresh";

    /* Vault Team */
    public const string PM8851_BrowserOnboardingNudge = "pm-8851-browser-onboarding-nudge";
    public const string PM9111ExtensionPersistAddEditForm = "pm-9111-extension-persist-add-edit-form";
    public const string RestrictProviderAccess = "restrict-provider-access";
    public const string SecurityTasks = "security-tasks";
    public const string CipherKeyEncryption = "cipher-key-encryption";
    public const string DesktopCipherForms = "pm-18520-desktop-cipher-forms";
    public const string PM19941MigrateCipherDomainToSdk = "pm-19941-migrate-cipher-domain-to-sdk";
    public const string EndUserNotifications = "pm-10609-end-user-notifications";
    public const string SeparateCustomRolePermissions = "pm-19917-separate-custom-role-permissions";
    public const string PhishingDetection = "phishing-detection";

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
