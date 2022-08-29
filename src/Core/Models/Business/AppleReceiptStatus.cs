using System.Text.Json.Serialization;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Utilities;

namespace Bit.Billing.Models;

public class AppleReceiptStatus
{
    [JsonPropertyName("status")]
    public int? Status { get; set; }
    [JsonPropertyName("environment")]
    public string Environment { get; set; }
    [JsonPropertyName("latest_receipt")]
    public string LatestReceipt { get; set; }
    [JsonPropertyName("receipt")]
    public AppleReceipt Receipt { get; set; }
    [JsonPropertyName("latest_receipt_info")]
    public List<AppleTransaction> LatestReceiptInfo { get; set; }
    [JsonPropertyName("pending_renewal_info")]
    public List<AppleRenewalInfo> PendingRenewalInfo { get; set; }

    public string GetOriginalTransactionId()
    {
        return LatestReceiptInfo?.LastOrDefault()?.OriginalTransactionId;
    }

    public string GetLastTransactionId()
    {
        return LatestReceiptInfo?.LastOrDefault()?.TransactionId;
    }

    public AppleTransaction GetLastTransaction()
    {
        return LatestReceiptInfo?.LastOrDefault();
    }

    public DateTime? GetLastExpiresDate()
    {
        return LatestReceiptInfo?.LastOrDefault()?.ExpiresDate;
    }

    public string GetReceiptData()
    {
        return LatestReceipt;
    }

    public DateTime? GetLastCancellationDate()
    {
        return LatestReceiptInfo?.LastOrDefault()?.CancellationDate;
    }

    public bool IsRefunded()
    {
        var cancellationDate = GetLastCancellationDate();
        var expiresDate = GetLastCancellationDate();
        if (cancellationDate.HasValue && expiresDate.HasValue)
        {
            return cancellationDate.Value <= expiresDate.Value;
        }
        return false;
    }

    public Transaction BuildTransactionFromLastTransaction(decimal amount, Guid userId)
    {
        return new Transaction
        {
            Amount = amount,
            CreationDate = GetLastTransaction().PurchaseDate,
            Gateway = GatewayType.AppStore,
            GatewayId = GetLastTransactionId(),
            UserId = userId,
            PaymentMethodType = PaymentMethodType.AppleInApp,
            Details = GetLastTransactionId()
        };
    }

    public class AppleReceipt
    {
        [JsonPropertyName("receipt_type")]
        public string ReceiptType { get; set; }
        [JsonPropertyName("bundle_id")]
        public string BundleId { get; set; }
        [JsonPropertyName("receipt_creation_date_ms")]
        [JsonConverter(typeof(MsEpochConverter))]
        public DateTime ReceiptCreationDate { get; set; }
        [JsonPropertyName("in_app")]
        public List<AppleTransaction> InApp { get; set; }
    }

    public class AppleRenewalInfo
    {
        [JsonPropertyName("expiration_intent")]
        public string ExpirationIntent { get; set; }
        [JsonPropertyName("auto_renew_product_id")]
        public string AutoRenewProductId { get; set; }
        [JsonPropertyName("original_transaction_id")]
        public string OriginalTransactionId { get; set; }
        [JsonPropertyName("is_in_billing_retry_period")]
        public string IsInBillingRetryPeriod { get; set; }
        [JsonPropertyName("product_id")]
        public string ProductId { get; set; }
        [JsonPropertyName("auto_renew_status")]
        public string AutoRenewStatus { get; set; }
    }

    public class AppleTransaction
    {
        [JsonPropertyName("quantity")]
        public string Quantity { get; set; }
        [JsonPropertyName("product_id")]
        public string ProductId { get; set; }
        [JsonPropertyName("transaction_id")]
        public string TransactionId { get; set; }
        [JsonPropertyName("original_transaction_id")]
        public string OriginalTransactionId { get; set; }
        [JsonPropertyName("purchase_date_ms")]
        [JsonConverter(typeof(MsEpochConverter))]
        public DateTime PurchaseDate { get; set; }
        [JsonPropertyName("original_purchase_date_ms")]
        [JsonConverter(typeof(MsEpochConverter))]
        public DateTime OriginalPurchaseDate { get; set; }
        [JsonPropertyName("expires_date_ms")]
        [JsonConverter(typeof(MsEpochConverter))]
        public DateTime ExpiresDate { get; set; }
        [JsonPropertyName("cancellation_date_ms")]
        [JsonConverter(typeof(MsEpochConverter))]
        public DateTime? CancellationDate { get; set; }
        [JsonPropertyName("web_order_line_item_id")]
        public string WebOrderLineItemId { get; set; }
        [JsonPropertyName("cancellation_reason")]
        public string CancellationReason { get; set; }
    }
}
