using System.Reflection;

namespace Bit.Core;

public static class Constants
{
    public const int BypassFiltersEventId = 12482444;

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

    public const string CipherKeyEncryptionMinimumVersion = "2023.9.1";
}

public static class AuthConstants
{
    public static readonly RangeConstant PBKDF2_ITERATIONS = new(600_000, 2_000_000, 100_000);

    public static readonly RangeConstant ARGON2_ITERATIONS = new(1, 10, 3);
    public static readonly RangeConstant ARGON2_MEMORY = new(15, 1024, 64);
    public static readonly RangeConstant ARGON_PARALLELISM = new(1, 16, 4);

}

public class RangeConstant
{
    public int Default { get; }
    public int Min;
    public int Max;

    public RangeConstant(int min, int max, int def)
    {
        Default = def;
        Min = min;
        Max = max;

        if (!IsInsideRange(def))
        {
            throw new ArgumentOutOfRangeException($"{Default} is outside allowed range of {Min}-{Max}.");
        }
    }

    public bool IsInsideRange(int number)
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
    public const string TrustedDeviceEncryption = "trusted-device-encryption";
    public const string AutofillV2 = "autofill-v2";
    public const string BrowserFilelessImport = "browser-fileless-import";

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
            { TrustedDeviceEncryption, "true" }
        };
    }
}
