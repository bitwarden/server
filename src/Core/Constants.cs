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
    public const string DisplayEuEnvironment = "display-eu-environment";
    public const string BrowserFilelessImport = "browser-fileless-import";
    public const string ReturnErrorOnExistingKeypair = "return-error-on-existing-keypair";
    public const string UseTreeWalkerApiForPageDetailsCollection = "use-tree-walker-api-for-page-details-collection";
    public const string ItemShare = "item-share";
    public const string DuoRedirect = "duo-redirect";
    public const string PM5864DollarThreshold = "PM-5864-dollar-threshold";
    public const string AC2101UpdateTrialInitiationEmail = "AC-2101-update-trial-initiation-email";
    public const string EnableConsolidatedBilling = "enable-consolidated-billing";
    public const string AC1795_UpdatedSubscriptionStatusSection = "AC-1795_updated-subscription-status-section";
    public const string EmailVerification = "email-verification";
    public const string EmailVerificationDisableTimingDelays = "email-verification-disable-timing-delays";
    public const string AnhFcmv1Migration = "anh-fcmv1-migration";
    public const string ExtensionRefresh = "extension-refresh";
    public const string RestrictProviderAccess = "restrict-provider-access";
    public const string PM4154BulkEncryptionService = "PM-4154-bulk-encryption-service";
    public const string VaultBulkManagementAction = "vault-bulk-management-action";
    public const string BulkDeviceApproval = "bulk-device-approval";
    public const string MemberAccessReport = "ac-2059-member-access-report";
    public const string BlockLegacyUsers = "block-legacy-users";
    public const string InlineMenuFieldQualification = "inline-menu-field-qualification";
    public const string TwoFactorComponentRefactor = "two-factor-component-refactor";
    public const string InlineMenuPositioningImprovements = "inline-menu-positioning-improvements";
    public const string ProviderClientVaultPrivacyBanner = "ac-2833-provider-client-vault-privacy-banner";
    public const string DeviceTrustLogging = "pm-8285-device-trust-logging";
    public const string AuthenticatorTwoFactorToken = "authenticator-2fa-token";
    public const string EnableUpgradePasswordManagerSub = "AC-2708-upgrade-password-manager-sub";
    public const string IdpAutoSubmitLogin = "idp-auto-submit-login";
    public const string UnauthenticatedExtensionUIRefresh = "unauth-ui-refresh";
    public const string GenerateIdentityFillScriptRefactor = "generate-identity-fill-script-refactor";
    public const string DelayFido2PageScriptInitWithinMv2 = "delay-fido2-page-script-init-within-mv2";
    public const string MembersTwoFAQueryOptimization = "ac-1698-members-two-fa-query-optimization";
    public const string NativeCarouselFlow = "native-carousel-flow";
    public const string NativeCreateAccountFlow = "native-create-account-flow";
    public const string AccountDeprovisioning = "pm-10308-account-deprovisioning";
    public const string NotificationBarAddLoginImprovements = "notification-bar-add-login-improvements";
    public const string AC2476_DeprecateStripeSourcesAPI = "AC-2476-deprecate-stripe-sources-api";
    public const string PersistPopupView = "persist-popup-view";
    public const string CipherKeyEncryption = "cipher-key-encryption";
    public const string EnableNewCardCombinedExpiryAutofill = "enable-new-card-combined-expiry-autofill";
    public const string StorageReseedRefactor = "storage-reseed-refactor";
    public const string TrialPayment = "PM-8163-trial-payment";
    public const string Pm3478RefactorOrganizationUserApi = "pm-3478-refactor-organizationuser-api";
    public const string RemoveServerVersionHeader = "remove-server-version-header";
    public const string LimitCollectionCreationDeletionSplit = "pm-10863-limit-collection-creation-deletion-split";

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
            { BulkDeviceApproval, "true" },
            { CipherKeyEncryption, "true" },
            { LimitCollectionCreationDeletionSplit, "true" },
        };
    }
}
