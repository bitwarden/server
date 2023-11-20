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

    public const string CipherKeyEncryptionMinimumVersion = "2023.9.2";

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
    /// Used by IdentityServer to identify our own provider.
    /// </summary>
    public const string IdentityProvider = "bitwarden";
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
    public const string AutofillV2 = "autofill-v2";
    public const string BrowserFilelessImport = "browser-fileless-import";
    public const string FlexibleCollections = "flexible-collections";
    public const string BulkCollectionAccess = "bulk-collection-access";
    public const string AutofillOverlay = "autofill-overlay";
    public const string ItemShare = "item-share";
    public const string BillingPlansUpgrade = "billing-plans-upgrade";
    public const string BillingStarterPlan = "billing-starter-plan";
    public const string KeyRotationImprovements = "key-rotation-improvements";

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
            { Fido2VaultCredentials, "true" }
        };
    }
}
