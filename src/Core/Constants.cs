// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

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
    public const string ScimInviteUserOptimization = "pm-16811-optimize-invite-user-flow-to-fail-fast";
    public const string EventBasedOrganizationIntegrations = "event-based-organization-integrations";
    public const string SeparateCustomRolePermissions = "pm-19917-separate-custom-role-permissions";
    public const string CreateDefaultLocation = "pm-19467-create-default-location";
    public const string DirectoryConnectorPreventUserRemoval = "pm-24592-directory-connector-prevent-user-removal";

    /* Auth Team */
    public const string PM9112DeviceApprovalPersistence = "pm-9112-device-approval-persistence";
    public const string TwoFactorExtensionDataPersistence = "pm-9115-two-factor-extension-data-persistence";
    public const string EmailVerification = "email-verification";
    public const string UnauthenticatedExtensionUIRefresh = "unauth-ui-refresh";
    public const string BrowserExtensionLoginApproval = "pm-14938-browser-extension-login-approvals";
    public const string SetInitialPasswordRefactor = "pm-16117-set-initial-password-refactor";
    public const string ChangeExistingPasswordRefactor = "pm-16117-change-existing-password-refactor";
    public const string RecoveryCodeLogin = "pm-17128-recovery-code-login";
    public const string Otp6Digits = "pm-18612-otp-6-digits";
    public const string FailedTwoFactorEmail = "pm-24425-send-2fa-failed-email";

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
    public const string WindowsDesktopAutotype = "windows-desktop-autotype";

    /* Billing Team */
    public const string AC2101UpdateTrialInitiationEmail = "AC-2101-update-trial-initiation-email";
    public const string TrialPayment = "PM-8163-trial-payment";
    public const string PM17772_AdminInitiatedSponsorships = "pm-17772-admin-initiated-sponsorships";
    public const string UsePricingService = "use-pricing-service";
    public const string PM12276Breadcrumbing = "pm-12276-breadcrumbing-for-business-features";
    public const string PM19422_AllowAutomaticTaxUpdates = "pm-19422-allow-automatic-tax-updates";
    public const string PM199566_UpdateMSPToChargeAutomatically = "pm-199566-update-msp-to-charge-automatically";
    public const string UseOrganizationWarningsService = "use-organization-warnings-service";
    public const string PM21881_ManagePaymentDetailsOutsideCheckout = "pm-21881-manage-payment-details-outside-checkout";
    public const string PM21821_ProviderPortalTakeover = "pm-21821-provider-portal-takeover";
    public const string PM22415_TaxIDWarnings = "pm-22415-tax-id-warnings";

    /* Key Management Team */
    public const string ReturnErrorOnExistingKeypair = "return-error-on-existing-keypair";
    public const string PM4154BulkEncryptionService = "PM-4154-bulk-encryption-service";
    public const string PrivateKeyRegeneration = "pm-12241-private-key-regeneration";
    public const string Argon2Default = "argon2-default";
    public const string UserkeyRotationV2 = "userkey-rotation-v2";
    public const string SSHKeyItemVaultItem = "ssh-key-vault-item";
    public const string UserSdkForDecryption = "use-sdk-for-decryption";
    public const string PM17987_BlockType0 = "pm-17987-block-type-0";
    public const string ForceUpdateKDFSettings = "pm-18021-force-update-kdf-settings";

    /* Mobile Team */
    public const string NativeCarouselFlow = "native-carousel-flow";
    public const string NativeCreateAccountFlow = "native-create-account-flow";
    public const string AndroidImportLoginsFlow = "import-logins-flow";
    public const string AppReviewPrompt = "app-review-prompt";
    public const string AndroidMutualTls = "mutual-tls";
    public const string SingleTapPasskeyCreation = "single-tap-passkey-creation";
    public const string SingleTapPasskeyAuthentication = "single-tap-passkey-authentication";
    public const string EnablePMAuthenticatorSync = "enable-pm-bwa-sync";
    public const string PM3503_MobileAnonAddySelfHostAlias = "anon-addy-self-host-alias";
    public const string PM3553_MobileSimpleLoginSelfHostAlias = "simple-login-self-host-alias";
    public const string EnablePMFlightRecorder = "enable-pm-flight-recorder";
    public const string MobileErrorReporting = "mobile-error-reporting";
    public const string AndroidChromeAutofill = "android-chrome-autofill";
    public const string UserManagedPrivilegedApps = "pm-18970-user-managed-privileged-apps";
    public const string EnablePMPreloginSettings = "enable-pm-prelogin-settings";
    public const string AppIntents = "app-intents";
    public const string SendAccess = "pm-19394-send-access-control";

    /* Platform Team */
    public const string PersistPopupView = "persist-popup-view";
    public const string StorageReseedRefactor = "storage-reseed-refactor";
    public const string WebPush = "web-push";
    public const string IpcChannelFramework = "ipc-channel-framework";
    public const string PushNotificationsWhenLocked = "pm-19388-push-notifications-when-locked";

    /* Tools Team */
    public const string DesktopSendUIRefresh = "desktop-send-ui-refresh";
    public const string UseSdkPasswordGenerators = "pm-19976-use-sdk-password-generators";
    public const string UseChromiumImporter = "pm-23982-chromium-importer";

    /* Vault Team */
    public const string PM8851_BrowserOnboardingNudge = "pm-8851-browser-onboarding-nudge";
    public const string PM9111ExtensionPersistAddEditForm = "pm-9111-extension-persist-add-edit-form";
    public const string SecurityTasks = "security-tasks";
    public const string CipherKeyEncryption = "cipher-key-encryption";
    public const string DesktopCipherForms = "pm-18520-desktop-cipher-forms";
    public const string PM19941MigrateCipherDomainToSdk = "pm-19941-migrate-cipher-domain-to-sdk";
    public const string EndUserNotifications = "pm-10609-end-user-notifications";
    public const string PhishingDetection = "phishing-detection";
    public const string RemoveCardItemTypePolicy = "pm-16442-remove-card-item-type-policy";
    public const string PM22134SdkCipherListView = "pm-22134-sdk-cipher-list-view";
    public const string PM19315EndUserActivationMvp = "pm-19315-end-user-activation-mvp";
    public const string PM22136_SdkCipherEncryption = "pm-22136-sdk-cipher-encryption";

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
