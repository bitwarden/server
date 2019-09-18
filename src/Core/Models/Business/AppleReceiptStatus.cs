using System;
using System.Collections.Generic;
using System.Linq;
using Bit.Core.Enums;
using Bit.Core.Models.Table;
using Bit.Core.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

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
        [JsonProperty("pending_renewal_info")]
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
            [JsonProperty("receipt_type")]
            public string ReceiptType { get; set; }
            [JsonProperty("bundle_id")]
            public string BundleId { get; set; }
            [JsonProperty("receipt_creation_date_ms")]
            [JsonConverter(typeof(MsEpochConverter))]
            public DateTime ReceiptCreationDate { get; set; }
            [JsonProperty("in_app")]
            public List<AppleTransaction> InApp { get; set; }
        }

        public class AppleRenewalInfo
        {
            [JsonProperty("expiration_intent")]
            public string ExpirationIntent { get; set; }
            [JsonProperty("auto_renew_product_id")]
            public string AutoRenewProductId { get; set; }
            [JsonProperty("original_transaction_id")]
            public string OriginalTransactionId { get; set; }
            [JsonProperty("is_in_billing_retry_period")]
            public string IsInBillingRetryPeriod { get; set; }
            [JsonProperty("product_id")]
            public string ProductId { get; set; }
            [JsonProperty("auto_renew_status")]
            public string AutoRenewStatus { get; set; }
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
            [JsonProperty("purchase_date_ms")]
            [JsonConverter(typeof(MsEpochConverter))]
            public DateTime PurchaseDate { get; set; }
            [JsonProperty("original_purchase_date_ms")]
            [JsonConverter(typeof(MsEpochConverter))]
            public DateTime OriginalPurchaseDate { get; set; }
            [JsonProperty("expires_date_ms")]
            [JsonConverter(typeof(MsEpochConverter))]
            public DateTime ExpiresDate { get; set; }
            [JsonProperty("web_order_line_item_id")]
            public string WebOrderLineItemId { get; set; }
        }

        public class MsEpochConverter : DateTimeConverterBase
        {
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                writer.WriteRawValue(CoreHelpers.ToEpocMilliseconds((DateTime)value).ToString());
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                return CoreHelpers.FromEpocMilliseconds(long.Parse(reader.Value.ToString()));
            }
        }
    }
}
