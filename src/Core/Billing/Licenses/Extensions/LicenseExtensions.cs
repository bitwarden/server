using System.Reflection;
using System.Security.Claims;
using System.Text;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Licenses.Attributes;
using Bit.Core.Models.Business;
using Bit.Core.Utilities;

namespace Bit.Core.Billing.Licenses.Extensions;

public static class LicenseExtensions
{
    public static DateTime CalculateFreshExpirationDate(this Organization org, SubscriptionInfo subscriptionInfo)
    {
        if (subscriptionInfo?.Subscription == null)
        {
            if (org.PlanType == PlanType.Custom && org.ExpirationDate.HasValue)
            {
                return org.ExpirationDate.Value;
            }

            return DateTime.UtcNow.AddDays(7);
        }

        var subscription = subscriptionInfo.Subscription;

        if (subscription.TrialEndDate > DateTime.UtcNow)
        {
            return subscription.TrialEndDate.Value;
        }

        if (org.ExpirationDate.HasValue && org.ExpirationDate.Value < DateTime.UtcNow)
        {
            return org.ExpirationDate.Value;
        }

        if (subscription.PeriodEndDate.HasValue && subscription.PeriodDuration > TimeSpan.FromDays(180))
        {
            return subscription.PeriodEndDate
                .Value
                .AddDays(Bit.Core.Constants.OrganizationSelfHostSubscriptionGracePeriodDays);
        }

        return org.ExpirationDate?.AddMonths(11) ?? DateTime.UtcNow.AddYears(1);
    }

    public static DateTime CalculateFreshRefreshDate(this Organization org, SubscriptionInfo subscriptionInfo, DateTime expirationDate)
    {
        if (subscriptionInfo?.Subscription == null ||
            subscriptionInfo.Subscription.TrialEndDate > DateTime.UtcNow ||
            org.ExpirationDate < DateTime.UtcNow)
        {
            return expirationDate;
        }

        return subscriptionInfo.Subscription.PeriodDuration > TimeSpan.FromDays(180) ||
            DateTime.UtcNow - expirationDate > TimeSpan.FromDays(30)
            ? DateTime.UtcNow.AddDays(30)
            : expirationDate;
    }

    public static DateTime CalculateFreshExpirationDateWithoutGracePeriod(this Organization org, SubscriptionInfo subscriptionInfo, DateTime expirationDate)
    {
        if (subscriptionInfo?.Subscription is null)
        {
            return expirationDate;
        }

        var subscription = subscriptionInfo.Subscription;

        if (subscription.TrialEndDate <= DateTime.UtcNow &&
            org.ExpirationDate >= DateTime.UtcNow &&
            subscription.PeriodEndDate.HasValue &&
            subscription.PeriodDuration > TimeSpan.FromDays(180))
        {
            return subscription.PeriodEndDate.Value;
        }

        return expirationDate;
    }

    public static byte[] GetDataBytesWithAttributes(this ILicense license, bool forHash = false)
    {
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

                return forHash && ignoreAttr.IncludeInHash;
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
