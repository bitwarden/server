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
    public const string DeviceApprovalRequestAdminNotifications = "pm-15637-device-approval-request-admin-notifications";
    public const string LimitItemDeletion = "pm-15493-restrict-item-deletion-to-can-manage-permission";
    public const string ShortcutDuplicatePatchRequests = "pm-16812-shortcut-duplicate-patch-requests";
    public const string PushSyncOrgKeysOnRevokeRestore = "pm-17168-push-sync-org-keys-on-revoke-restore";
    public const string PolicyRequirements = "pm-14439-policy-requirements";

    /* Tools Team */
    public const string ItemShare = "item-share";
    public const string RiskInsightsCriticalApplication = "pm-14466-risk-insights-critical-application";
    public const string EnableRiskInsightsNotifications = "enable-risk-insights-notifications";
    public const string DesktopSendUIRefresh = "desktop-send-ui-refresh";

    public const string ReturnErrorOnExistingKeypair = "return-error-on-existing-keypair";
    public const string UseTreeWalkerApiForPageDetailsCollection = "use-tree-walker-api-for-page-details-collection";
    public const string DuoRedirect = "duo-redirect";
    public const string AC2101UpdateTrialInitiationEmail = "AC-2101-update-trial-initiation-email";
    public const string EmailVerification = "email-verification";
    public const string EmailVerificationDisableTimingDelays = "email-verification-disable-timing-delays";
    public const string RestrictProviderAccess = "restrict-provider-access";
    public const string PM4154BulkEncryptionService = "PM-4154-bulk-encryption-service";
    public const string VaultBulkManagementAction = "vault-bulk-management-action";
    public const string InlineMenuFieldQualification = "inline-menu-field-qualification";
    public const string InlineMenuPositioningImprovements = "inline-menu-positioning-improvements";
    public const string DeviceTrustLogging = "pm-8285-device-trust-logging";
    public const string SSHKeyItemVaultItem = "ssh-key-vault-item";
    public const string SSHAgent = "ssh-agent";
    public const string SSHVersionCheckQAOverride = "ssh-version-check-qa-override";
    public const string AuthenticatorTwoFactorToken = "authenticator-2fa-token";
    public const string IdpAutoSubmitLogin = "idp-auto-submit-login";
    public const string UnauthenticatedExtensionUIRefresh = "unauth-ui-refresh";
    public const string GenerateIdentityFillScriptRefactor = "generate-identity-fill-script-refactor";
    public const string DelayFido2PageScriptInitWithinMv2 = "delay-fido2-page-script-init-within-mv2";
    public const string NativeCarouselFlow = "native-carousel-flow";
    public const string NativeCreateAccountFlow = "native-create-account-flow";
    public const string NotificationBarAddLoginImprovements = "notification-bar-add-login-improvements";
    public const string BlockBrowserInjectionsByDomain = "block-browser-injections-by-domain";
    public const string NotificationRefresh = "notification-refresh";
    public const string PersistPopupView = "persist-popup-view";
    public const string CipherKeyEncryption = "cipher-key-encryption";
    public const string EnableNewCardCombinedExpiryAutofill = "enable-new-card-combined-expiry-autofill";
    public const string StorageReseedRefactor = "storage-reseed-refactor";
    public const string TrialPayment = "PM-8163-trial-payment";
    public const string RemoveServerVersionHeader = "remove-server-version-header";
    public const string GeneratorToolsModernization = "generator-tools-modernization";
    public const string NewDeviceVerification = "new-device-verification";
    public const string NewDeviceVerificationTemporaryDismiss = "new-device-temporary-dismiss";
    public const string NewDeviceVerificationPermanentDismiss = "new-device-permanent-dismiss";
    public const string SecurityTasks = "security-tasks";
    public const string MacOsNativeCredentialSync = "macos-native-credential-sync";
    public const string PM9111ExtensionPersistAddEditForm = "pm-9111-extension-persist-add-edit-form";
    public const string InlineMenuTotp = "inline-menu-totp";
    public const string PrivateKeyRegeneration = "pm-12241-private-key-regeneration";
    public const string AppReviewPrompt = "app-review-prompt";
    public const string ResellerManagedOrgAlert = "PM-15814-alert-owners-of-reseller-managed-orgs";
    public const string Argon2Default = "argon2-default";
    public const string UsePricingService = "use-pricing-service";
    public const string RecordInstallationLastActivityDate = "installation-last-activity-date";
    public const string AccountDeprovisioningBanner = "pm-17120-account-deprovisioning-admin-console-banner";
    public const string SingleTapPasskeyCreation = "single-tap-passkey-creation";
    public const string SingleTapPasskeyAuthentication = "single-tap-passkey-authentication";
    public const string EnablePMAuthenticatorSync = "enable-pm-bwa-sync";
    public const string P15179_AddExistingOrgsFromProviderPortal = "pm-15179-add-existing-orgs-from-provider-portal";
    public const string AndroidMutualTls = "mutual-tls";
    public const string RecoveryCodeLogin = "pm-17128-recovery-code-login";
    public const string PM3503_MobileAnonAddySelfHostAlias = "anon-addy-self-host-alias";
    public const string WebPush = "web-push";
    public const string AndroidImportLoginsFlow = "import-logins-flow";
    public const string PM12276Breadcrumbing = "pm-12276-breadcrumbing-for-business-features";

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
        return new Dictionary<string, string>()
        {
            { DuoRedirect, "true" },
        };
    }
}
