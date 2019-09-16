using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Bit.Billing.Models
{
    public class AppleReceiptStatus
    {
        [JsonProperty("status")]
        public int? Status { get; set; }
        [JsonProperty("environment")]
        public string Environment { get; set; }
        [JsonProperty("latest_receipt")]
        public string LatestReceipt { get; set; }
        [JsonProperty("receipt")]
        public AppleReceipt Receipt { get; set; }
        [JsonProperty("latest_receipt_info")]
        public List<AppleTransaction> LatestReceiptInfo { get; set; }
        /*
        [JsonProperty("latest_expired_receipt_info")]
        public AppleReceipt LatestExpiredReceiptInfo { get; set; }
        [JsonProperty("auto_renew_status")]
        public string AutoRenewStatus { get; set; }
        [JsonProperty("auto_renew_product_id")]
        public string AutoRenewProductId { get; set; }
        [JsonProperty("notification_type")]
        public string NotificationType { get; set; }
        [JsonProperty("expiration_intent")]
        public string ExpirationIntent { get; set; }
        [JsonProperty("is_in_billing_retry_period")]
        public string IsInBillingRetryPeriod { get; set; }
        */

        public class AppleReceipt
        {
            [JsonProperty("receipt_type")]
            public string ReceiptType { get; set; }
            [JsonProperty("bundle_id")]
            public string BundleId { get; set; }
            [JsonProperty("receipt_creation_date")]
            public DateTime ReceiptCreationDate { get; set; }
            [JsonProperty("in_app")]
            public List<AppleTransaction> InApp { get; set; }
        }

        public class AppleTransaction
        {
            [JsonProperty("quantity")]
            public string Quantity { get; set; }
            [JsonProperty("product_id")]
            public string ProductId { get; set; }
            [JsonProperty("transaction_id")]
            public string TransactionId { get; set; }
            [JsonProperty("original_transaction_id")]
            public string OriginalTransactionId { get; set; }
            [JsonProperty("purchase_date")]
            public DateTime PurchaseDate { get; set; }
            [JsonProperty("original_purchase_date")]
            public DateTime OriginalPurchaseDate { get; set; }
            [JsonProperty("expires_date")]
            public DateTime ExpiresDate { get; set; }
            [JsonProperty("web_order_line_item_id")]
            public string WebOrderLineItemId { get; set; }
        }
    }
}
