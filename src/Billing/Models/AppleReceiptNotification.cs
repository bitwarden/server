using System;
using Bit.Core.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Bit.Billing.Models
{
    public class AppleReceiptNotification
    {
        [JsonProperty("notification_type")]
        public string NotificationType { get; set; }
        [JsonProperty("environment")]
        public string Environment { get; set; }
        [JsonProperty("auto_renew_status")]
        public string AutoRenewStatus { get; set; }
        [JsonProperty("auto_renew_product_id")]
        public string AutoRenewProductId { get; set; }
        [JsonProperty("auto_renew_status_change_date_ms")]
        [JsonConverter(typeof(MsEpochConverter))]
        public DateTime? AutoRenewStatusChangeDate { get; set; }
        [JsonProperty("latest_receipt")]
        public string LatestReceipt { get; set; }
        [JsonProperty("latest_receipt_info")]
        public AppleReceiptNotificationInfo LatestReceiptInfo { get; set; }
        [JsonProperty("latest_expired_receipt")]
        public string LatestExpiredReceipt { get; set; }
        [JsonProperty("latest_expired_receipt_info")]
        public AppleReceiptNotificationInfo LatestExpiredReceiptInfo { get; set; }

        public string GetOriginalTransactionId()
        {
            if(LatestReceiptInfo != null)
            {
                return LatestReceiptInfo.OriginalTransactionId;
            }
            return LatestExpiredReceiptInfo?.OriginalTransactionId;
        }

        public string GetTransactionId()
        {
            if(LatestReceiptInfo != null)
            {
                return LatestReceiptInfo.TransactionId;
            }
            return LatestExpiredReceiptInfo?.TransactionId;
        }

        public DateTime? GetExpiresDate()
        {
            if(LatestReceiptInfo != null)
            {
                return LatestReceiptInfo.ExpiresDate;
            }
            return LatestExpiredReceiptInfo?.ExpiresDate;
        }

        public string GetReceiptData()
        {
            return string.IsNullOrWhiteSpace(LatestReceipt) ? LatestExpiredReceipt : LatestReceipt;
        }

        public class AppleReceiptNotificationInfo
        {
            [JsonProperty("bid")]
            public string Bid { get; set; }
            public string ProductId { get; set; }
            [JsonProperty("original_purchase_date_ms")]
            [JsonConverter(typeof(MsEpochConverter))]
            public DateTime? OriginalPurchaseDate { get; set; }
            [JsonProperty("expires_date")]
            [JsonConverter(typeof(MsEpochConverter))]
            public DateTime? ExpiresDate { get; set; }
            [JsonProperty("purchase_date_ms")]
            [JsonConverter(typeof(MsEpochConverter))]
            public DateTime? PurchaseDate { get; set; }
            [JsonProperty("subscription_group_identifier")]
            public string SubscriptionGroupIdentifier { get; set; }
            [JsonProperty("unique_identifier")]
            public string UniqueIdentifier { get; set; }
            [JsonProperty("original_transaction_id")]
            public string OriginalTransactionId { get; set; }
            [JsonProperty("transaction_id")]
            public string TransactionId { get; set; }
            [JsonProperty("quantity")]
            public string Quantity { get; set; }
            [JsonProperty("web_order_line_item_id")]
            public string WebOrderLineItemId { get; set; }
            [JsonProperty("item_id")]
            public string ItemId { get; set; }
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
