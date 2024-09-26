using System.Globalization;
using System.Runtime.InteropServices;
using System.Web;

namespace Bit.Billing.Models;

public class PayPalIPNTransactionModel
{
    public string TransactionId { get; }
    public string TransactionType { get; }
    public string ParentTransactionId { get; }
    public string PaymentStatus { get; }
    public string PaymentType { get; }
    public decimal MerchantGross { get; }
    public string MerchantCurrency { get; }
    public string ReceiverId { get; }
    public DateTime PaymentDate { get; }
    public Guid? UserId { get; }
    public Guid? OrganizationId { get; }
    public Guid? ProviderId { get; }
    public bool IsAccountCredit { get; }

    public PayPalIPNTransactionModel(string formData)
    {
        var queryString = HttpUtility.ParseQueryString(formData);

        var data = queryString
            .AllKeys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .ToDictionary(key =>
                key.Trim('\r'),
                key => queryString[key]?.Trim('\r'));

        TransactionId = Extract(data, "txn_id");
        TransactionType = Extract(data, "txn_type");
        ParentTransactionId = Extract(data, "parent_txn_id");
        PaymentStatus = Extract(data, "payment_status");
        PaymentType = Extract(data, "payment_type");

        var merchantGross = Extract(data, "mc_gross");
        if (!string.IsNullOrEmpty(merchantGross))
        {
            MerchantGross = decimal.Parse(merchantGross);
        }

        MerchantCurrency = Extract(data, "mc_currency");
        ReceiverId = Extract(data, "receiver_id");

        var paymentDate = Extract(data, "payment_date");
        PaymentDate = ToUTCDateTime(paymentDate);

        var custom = Extract(data, "custom");

        if (string.IsNullOrEmpty(custom))
        {
            return;
        }

        var metadata = custom.Split(',')
            .Where(field => !string.IsNullOrEmpty(field) && field.Contains(':'))
            .Select(field => field.Split(':'))
            .ToDictionary(parts => parts[0], parts => parts[1]);

        if (metadata.TryGetValue("user_id", out var userIdStr) &&
            Guid.TryParse(userIdStr, out var userId))
        {
            UserId = userId;
        }

        if (metadata.TryGetValue("organization_id", out var organizationIdStr) &&
            Guid.TryParse(organizationIdStr, out var organizationId))
        {
            OrganizationId = organizationId;
        }

        if (metadata.TryGetValue("provider_id", out var providerIdStr) &&
            Guid.TryParse(providerIdStr, out var providerId))
        {
            ProviderId = providerId;
        }

        IsAccountCredit = custom.Contains("account_credit:1");
    }

    private static string Extract(IReadOnlyDictionary<string, string> data, string key)
    {
        var success = data.TryGetValue(key, out var value);
        return success ? value : null;
    }

    private static DateTime ToUTCDateTime(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return default;
        }

        var success = DateTime.TryParseExact(input,
            new[]
            {
                "HH:mm:ss dd MMM yyyy PDT",
                "HH:mm:ss dd MMM yyyy PST",
                "HH:mm:ss dd MMM, yyyy PST",
                "HH:mm:ss dd MMM, yyyy PDT",
                "HH:mm:ss MMM dd, yyyy PST",
                "HH:mm:ss MMM dd, yyyy PDT"
            }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTime);

        if (!success)
        {
            return default;
        }

        var pacificTime = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time")
            : TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles");

        return TimeZoneInfo.ConvertTimeToUtc(dateTime, pacificTime);
    }
}
