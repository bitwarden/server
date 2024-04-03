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

    public const string CipherKeyEncryptionMinimumVersion = "2024.2.0";

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
    public const string DisplayLowKdfIterationWarning = "display-kdf-iteration-warning";
    public const string PasswordlessLogin = "passwordless-login";
    public const string TrustedDeviceEncryption = "trusted-device-encryption";
    public const string Fido2VaultCredentials = "fido2-vault-credentials";
    public const string VaultOnboarding = "vault-onboarding";
    public const string BrowserFilelessImport = "browser-fileless-import";
    /// <summary>
    /// Deprecated - never used, do not use. Will always default to false. Will be deleted as part of Flexible Collections cleanup
    /// </summary>
    public const string FlexibleCollections = "flexible-collections-disabled-do-not-use";
    public const string FlexibleCollectionsV1 = "flexible-collections-v-1"; // v-1 is intentional
    public const string ItemShare = "item-share";
    public const string KeyRotationImprovements = "key-rotation-improvements";
    public const string DuoRedirect = "duo-redirect";
    /// <summary>
    /// Enables flexible collections improvements for new organizations on creation
    /// </summary>
    public const string FlexibleCollectionsSignup = "flexible-collections-signup";
    /// <summary>
    /// Exposes a migration button in the web vault which allows users to migrate an existing organization to
    /// flexible collections
    /// </summary>
    public const string FlexibleCollectionsMigration = "flexible-collections-migration";
    public const string PM5766AutomaticTax = "PM-5766-automatic-tax";
    public const string PM5864DollarThreshold = "PM-5864-dollar-threshold";
    public const string AC2101UpdateTrialInitiationEmail = "AC-2101-update-trial-initiation-email";
    public const string ShowPaymentMethodWarningBanners = "show-payment-method-warning-banners";
    public const string EnableConsolidatedBilling = "enable-consolidated-billing";
    public const string AC1795_UpdatedSubscriptionStatusSection = "AC-1795_updated-subscription-status-section";

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
            { TrustedDeviceEncryption, "true" },
            { Fido2VaultCredentials, "true" },
            { DuoRedirect, "true" },
            { FlexibleCollectionsSignup, "true" }
        };
    }
}
