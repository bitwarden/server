using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Licenses.Attributes;
using Bit.Core.Entities;
using Bit.Core.Models.Business;
using Bit.Core.Utilities;

namespace Bit.Core.Billing.Licenses.Extensions;

public static class LicenseExtensions
{
    public static byte[] ComputeHash(this ILicense license) => SHA256.HashData(license.ToByteArray(true));

    public static bool VerifySignature(this ILicense license, X509Certificate2 certificate)
    {
        var dataBytes = license.ToByteArray();
        var signatureBytes = Convert.FromBase64String(license.Signature);
        using var rsa = certificate.GetRSAPublicKey();

        return rsa.VerifyData(dataBytes, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }

    public static byte[] Sign(this ILicense license, X509Certificate2 certificate)
    {
        if (!certificate.HasPrivateKey)
        {
            throw new InvalidOperationException("You don't have the private key!");
        }

        var dataBytes = license.ToByteArray();
        using var rsa = certificate.GetRSAPrivateKey();

        return rsa.SignData(dataBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }

    public static byte[] ToByteArray(this ILicense license, bool forHash = false)
    {
        if (!license.ValidLicenseVersion)
        {
            throw new NotSupportedException($"Version {license.Version} is not supported.");
        }

        var props = license.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p =>
            {
                var versionAttr = p.GetCustomAttribute<LicenseVersionAttribute>();
                if (versionAttr is null || versionAttr.Version > license.Version)
                {
                    return false;
                }

                var ignoreAttr = p.GetCustomAttribute<LicenseIgnoreAttribute>();
                if (ignoreAttr is null)
                {
                    return true;
                }

                return !forHash && ignoreAttr.IncludeInSignature;
            })
            .OrderBy(p => p.Name)
            .Select(p => $"{p.Name}:{CoreHelpers.FormatLicenseSignatureValue(p.GetValue(license, null))}")
            .Aggregate((c, n) => $"{c}|{n}");

        var data = $"license:{license.LicenseType.ToString().ToLowerInvariant()}|{props}";
        return Encoding.UTF8.GetBytes(data);
    }

    public static T GetValue<T>(this ClaimsPrincipal principal, string claimType)
    {
        var claim = principal.FindFirst(claimType);

        if (claim is null)
        {
            return default;
        }

        // Handle Guid
        if (typeof(T) == typeof(Guid))
        {
            return Guid.TryParse(claim.Value, out var guid)
                ? (T)(object)guid
                : default;
        }

        // Handle DateTime
        if (typeof(T) == typeof(DateTime))
        {
            return DateTime.TryParse(claim.Value, out var dateTime)
                ? (T)(object)dateTime
                : default;
        }

        // Handle TimeSpan
        if (typeof(T) == typeof(TimeSpan))
        {
            return TimeSpan.TryParse(claim.Value, out var timeSpan)
                ? (T)(object)timeSpan
                : default;
        }

        // Check for Nullable Types
        var underlyingType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);

        // Handle Enums
        if (underlyingType.IsEnum)
        {
            if (Enum.TryParse(underlyingType, claim.Value, true, out var enumValue))
            {
                return (T)enumValue; // Cast back to T
            }

            return default; // Return default value for non-nullable enums or null for nullable enums
        }

        // Handle other Nullable Types (e.g., int?, bool?)
        if (underlyingType == typeof(int))
        {
            return int.TryParse(claim.Value, out var intValue)
                ? (T)(object)intValue
                : default;
        }

        if (underlyingType == typeof(bool))
        {
            return bool.TryParse(claim.Value, out var boolValue)
                ? (T)(object)boolValue
                : default;
        }

        if (underlyingType == typeof(double))
        {
            return double.TryParse(claim.Value, out var doubleValue)
                ? (T)(object)doubleValue
                : default;
        }

        // Fallback to Convert.ChangeType for other types including strings
        return (T)Convert.ChangeType(claim.Value, underlyingType);
    }
}

public static class OrganizationLicenseExtensions
{
    public static DateTime CalculateFreshExpirationDate(this Organization org, SubscriptionInfo subscriptionInfo, DateTime issued)
    {
        if (subscriptionInfo?.Subscription == null)
        {
            return org.PlanType == PlanType.Custom && org.ExpirationDate.HasValue
                ? org.ExpirationDate.Value
                : issued.AddDays(7);
        }

        if (subscriptionInfo.Subscription.TrialEndDate.HasValue &&
                 subscriptionInfo.Subscription.TrialEndDate.Value > DateTime.UtcNow)
        {
            return subscriptionInfo.Subscription.TrialEndDate.Value;
        }

        if (org.ExpirationDate.HasValue && org.ExpirationDate.Value < DateTime.UtcNow)
        {
            // expired
            return org.ExpirationDate.Value;
        }

        if (subscriptionInfo?.Subscription?.PeriodDuration != null &&
                    subscriptionInfo.Subscription.PeriodDuration > TimeSpan.FromDays(180))
        {
            return subscriptionInfo.Subscription.PeriodEndDate.Value.AddDays(Core.Constants.OrganizationSelfHostSubscriptionGracePeriodDays);
        }

        return org.ExpirationDate.HasValue
            ? org.ExpirationDate.Value.AddMonths(11)
            : issued.AddYears(1);
    }

    public static DateTime CalculateFreshRefreshDate(this Organization org, SubscriptionInfo subscriptionInfo, DateTime? expirationDate, DateTime issued)
    {
        if (subscriptionInfo?.Subscription == null)
        {
            return org.PlanType == PlanType.Custom && org.ExpirationDate.HasValue
                ? org.ExpirationDate.Value
                : issued.AddDays(7);
        }

        if (subscriptionInfo.Subscription.TrialEndDate.HasValue &&
                 subscriptionInfo.Subscription.TrialEndDate.Value > DateTime.UtcNow)
        {
            return subscriptionInfo.Subscription.TrialEndDate.Value;
        }

        if (org.ExpirationDate.HasValue && org.ExpirationDate.Value < DateTime.UtcNow)
        {
            return org.ExpirationDate.Value;
        }

        if (subscriptionInfo?.Subscription?.PeriodDuration != null &&
                    subscriptionInfo.Subscription.PeriodDuration > TimeSpan.FromDays(180))
        {
            return DateTime.UtcNow.AddDays(30);
        }

        return !expirationDate.HasValue || DateTime.UtcNow - expirationDate.Value > TimeSpan.FromDays(30)
            ? DateTime.UtcNow.AddDays(30)
            : expirationDate.Value;
    }

    public static DateTime? CalculateFreshExpirationDateWithoutGracePeriod(this Organization org, SubscriptionInfo subscriptionInfo) =>
        subscriptionInfo?.Subscription != null &&
        (!subscriptionInfo.Subscription.TrialEndDate.HasValue || subscriptionInfo.Subscription.TrialEndDate.Value <= DateTime.UtcNow) &&
        (!org.ExpirationDate.HasValue || org.ExpirationDate.Value >= DateTime.UtcNow) &&
        subscriptionInfo.Subscription.PeriodDuration != null &&
        subscriptionInfo.Subscription.PeriodDuration > TimeSpan.FromDays(180)
            ? subscriptionInfo.Subscription.PeriodEndDate
            : null;

    public static bool IsTrialing(this Organization org, SubscriptionInfo subscriptionInfo) =>
        subscriptionInfo?.Subscription == null
            ? org.PlanType != PlanType.Custom || !org.ExpirationDate.HasValue
            : subscriptionInfo.Subscription.TrialEndDate.HasValue && subscriptionInfo.Subscription.TrialEndDate.Value > DateTime.UtcNow;
}

public static class UserLicenseExtensions
{
    public static DateTime? CalculateFreshExpirationDate(this User user, SubscriptionInfo subscriptionInfo) =>
        subscriptionInfo == null
            ? user.PremiumExpirationDate?.AddDays(7)
            : subscriptionInfo.UpcomingInvoice?.Date != null
                ? subscriptionInfo.UpcomingInvoice.Date.Value.AddDays(7)
                : user.PremiumExpirationDate?.AddDays(7);

    public static DateTime? CalculateFreshRefreshDate(this User user, SubscriptionInfo subscriptionInfo) =>
        subscriptionInfo == null
            ? user.PremiumExpirationDate?.Date
            : subscriptionInfo?.UpcomingInvoice?.Date;

    public static bool IsTrialing(this User user, SubscriptionInfo subscriptionInfo) =>
        subscriptionInfo != null &&
            (subscriptionInfo?.Subscription?.TrialEndDate.HasValue ?? false) &&
            subscriptionInfo.Subscription.TrialEndDate.Value > DateTime.UtcNow;
}
