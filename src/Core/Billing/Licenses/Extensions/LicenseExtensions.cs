// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.Security.Claims;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Models.Business;

namespace Bit.Core.Billing.Licenses.Extensions;

public static class LicenseExtensions
{
    public static DateTime CalculateFreshExpirationDate(this Organization org, SubscriptionInfo subscriptionInfo, DateTime issued)
    {

        if (subscriptionInfo?.Subscription == null)
        {
            // Subscription isn't setup yet, so fallback to the organization's expiration date
            // If there isn't an expiration date on the org, treat it as a free trial
            return org.ExpirationDate ?? issued.AddDays(7);
        }

        var subscription = subscriptionInfo.Subscription;

        if (subscription.TrialEndDate > DateTime.UtcNow)
        {
            // Still trialing, use trial's end date
            return subscription.TrialEndDate.Value;
        }

        if (org.ExpirationDate < DateTime.UtcNow)
        {
            // Organization is expired
            return org.ExpirationDate.Value;
        }

        if (subscription.PeriodDuration > TimeSpan.FromDays(180))
        {
            // Annual subscription - include grace period to give the administrators time to upload a new license
            return subscription.PeriodEndDate
                !.Value
                .AddDays(Core.Constants.OrganizationSelfHostSubscriptionGracePeriodDays);
        }

        // Monthly subscription - giving an annual expiration to not burnden admins to upload fresh licenses each month
        return org.ExpirationDate?.AddMonths(11) ?? issued.AddYears(1);
    }

    public static DateTime CalculateFreshRefreshDate(this Organization org, SubscriptionInfo subscriptionInfo, DateTime issued)
    {

        if (subscriptionInfo?.Subscription == null)
        {
            // Subscription isn't setup yet, so fallback to the organization's expiration date
            // If there isn't an expiration date on the org, treat it as a free trial
            return org.ExpirationDate ?? issued.AddDays(7);
        }

        var subscription = subscriptionInfo.Subscription;

        if (subscription.TrialEndDate > DateTime.UtcNow)
        {
            // Still trialing, use trial's end date
            return subscription.TrialEndDate.Value;
        }

        if (org.ExpirationDate < DateTime.UtcNow)
        {
            // Organization is expired
            return org.ExpirationDate.Value;
        }

        if (subscription.PeriodDuration > TimeSpan.FromDays(180))
        {
            // Annual subscription - refresh every 30 days to check for plan changes, cancellations, and payment issues
            return issued.AddDays(30);
        }

        var expires = org.ExpirationDate?.AddMonths(11) ?? issued.AddYears(1);

        // If expiration is more than 30 days in the past, refresh in 30 days instead of using the stale date to give
        // them a chance to refresh. Otherwise, uses the expiration date
        return issued - expires > TimeSpan.FromDays(30)
            ? issued.AddDays(30)
            : expires;
    }

    public static DateTime? CalculateFreshExpirationDateWithoutGracePeriod(this Organization org, SubscriptionInfo subscriptionInfo)
    {
        // It doesn't make sense that this returns null sometimes. If the expiration date doesn't include a grace period
        // then we should just return the expiration date instead of null. This is currently forcing the single consumer
        // to check for nulls.

        // At some point in the future, we should update this. We can't easily, though, without breaking the signatures
        // since `ExpirationWithoutGracePeriod` is included on them. So for now, I'll shake my fist and then move on.

        // Only set expiration without grace period for active, non-trial, annual subscriptions
        if (subscriptionInfo?.Subscription != null &&
            subscriptionInfo.Subscription.TrialEndDate <= DateTime.UtcNow &&
            org.ExpirationDate >= DateTime.UtcNow &&
            subscriptionInfo.Subscription.PeriodDuration > TimeSpan.FromDays(180))
        {
            return subscriptionInfo.Subscription.PeriodEndDate;
        }

        // Otherwise, return null.
        return null;
    }

    public static bool CalculateIsTrialing(this Organization org, SubscriptionInfo subscriptionInfo) =>
        subscriptionInfo?.Subscription is null
            ? !org.ExpirationDate.HasValue
            : subscriptionInfo.Subscription.TrialEndDate > DateTime.UtcNow;

    public static T GetValue<T>(this ClaimsPrincipal principal, string claimType)
    {
        var claim = principal.FindFirst(claimType);

        if (claim is null || (string.IsNullOrWhiteSpace(claim.Value) && typeof(T).IsValueType))
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

        // Handle DateTimeOffset
        if (typeof(T) == typeof(DateTimeOffset))
        {
            return DateTimeOffset.TryParse(claim.Value, out var dateTimeOffset)
                ? (T)(object)dateTimeOffset
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

        if (underlyingType == typeof(char))
        {
            return char.TryParse(claim.Value, out var charValue)
                ? (T)(object)charValue
                : default;
        }

        if (underlyingType == typeof(double))
        {
            return double.TryParse(claim.Value, out var doubleValue)
                ? (T)(object)doubleValue
                : default;
        }

        if (underlyingType == typeof(short))
        {
            return short.TryParse(claim.Value, out var shortValue)
                ? (T)(object)shortValue
                : default;
        }

        if (underlyingType == typeof(long))
        {
            return long.TryParse(claim.Value, out var longValue)
                ? (T)(object)longValue
                : default;
        }

        if (underlyingType == typeof(decimal))
        {
            return decimal.TryParse(claim.Value, out var decimalValue)
                ? (T)(object)decimalValue
                : default;
        }

        if (underlyingType == typeof(float))
        {
            return float.TryParse(claim.Value, out var floatValue)
                ? (T)(object)floatValue
                : default;
        }

        if (underlyingType == typeof(byte))
        {
            return byte.TryParse(claim.Value, out var byteValue)
                ? (T)(object)byteValue
                : default;
        }

        if (underlyingType == typeof(DateTime))
        {
            return DateTime.TryParse(claim.Value, out var dateTimeValue)
                ? (T)(object)dateTimeValue
                : default;
        }

        if (underlyingType == typeof(DateTimeOffset))
        {
            return DateTimeOffset.TryParse(claim.Value, out var dateTimeOffsetValue)
                ? (T)(object)dateTimeOffsetValue
                : default;
        }

        if (underlyingType == typeof(Guid))
        {
            return Guid.TryParse(claim.Value, out var guidValue)
                ? (T)(object)guidValue
                : default;
        }

        if (underlyingType == typeof(TimeSpan))
        {
            return TimeSpan.TryParse(claim.Value, out var timeSpanValue)
                ? (T)(object)timeSpanValue
                : default;
        }

        if (underlyingType == typeof(uint))
        {
            return uint.TryParse(claim.Value, out var uintValue)
                ? (T)(object)uintValue
                : default;
        }

        if (underlyingType == typeof(ushort))
        {
            return ushort.TryParse(claim.Value, out var ushortValue)
                ? (T)(object)ushortValue
                : default;
        }

        if (underlyingType == typeof(ulong))
        {
            return ulong.TryParse(claim.Value, out var ulongValue)
                ? (T)(object)ulongValue
                : default;
        }

        if (underlyingType == typeof(sbyte))
        {
            return sbyte.TryParse(claim.Value, out var sbyteValue)
                ? (T)(object)sbyteValue
                : default;
        }

        // Fallback to Convert.ChangeType for other types including strings
        try
        {
            // If the value is empty and we're not converting to string, return default
            if (string.IsNullOrWhiteSpace(claim.Value) && underlyingType != typeof(string))
            {
                return default;
            }

            return (T)Convert.ChangeType(claim.Value, underlyingType);
        }
        catch
        {
            // If conversion fails for any reason, return default value
            return default;
        }
    }
}
