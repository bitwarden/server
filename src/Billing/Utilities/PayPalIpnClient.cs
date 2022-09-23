using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Web;
using Microsoft.Extensions.Options;

namespace Bit.Billing.Utilities;

public class PayPalIpnClient
{
    private readonly HttpClient _httpClient = new HttpClient();
    private readonly Uri _ipnUri;

    public PayPalIpnClient(IOptions<BillingSettings> billingSettings)
    {
        var bSettings = billingSettings?.Value;
        _ipnUri = new Uri(bSettings.PayPal.Production ? "https://www.paypal.com/cgi-bin/webscr" :
            "https://www.sandbox.paypal.com/cgi-bin/webscr");
    }

    public async Task<bool> VerifyIpnAsync(string ipnBody)
    {
        if (ipnBody == null)
        {
            throw new ArgumentException("No IPN body.");
        }

        var request = new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            RequestUri = _ipnUri
        };
        var cmdIpnBody = string.Concat("cmd=_notify-validate&", ipnBody);
        request.Content = new StringContent(cmdIpnBody, Encoding.UTF8, "application/x-www-form-urlencoded");
        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception("Failed to verify IPN, status: " + response.StatusCode);
        }
        var responseContent = await response.Content.ReadAsStringAsync();
        if (responseContent.Equals("VERIFIED"))
        {
            return true;
        }
        else if (responseContent.Equals("INVALID"))
        {
            return false;
        }
        else
        {
            throw new Exception("Failed to verify IPN.");
        }
    }

    public class IpnTransaction
    {
        private string[] _dateFormats = new string[]
        {
            "HH:mm:ss dd MMM yyyy PDT", "HH:mm:ss dd MMM yyyy PST", "HH:mm:ss dd MMM, yyyy PST",
            "HH:mm:ss dd MMM, yyyy PDT","HH:mm:ss MMM dd, yyyy PST", "HH:mm:ss MMM dd, yyyy PDT"
        };

        public IpnTransaction(string ipnFormData)
        {
            if (string.IsNullOrWhiteSpace(ipnFormData))
            {
                return;
            }

            var qsData = HttpUtility.ParseQueryString(ipnFormData);
            var dataDict = qsData.Keys.Cast<string>().ToDictionary(k => k, v => qsData[v].ToString());

            TxnId = GetDictValue(dataDict, "txn_id");
            TxnType = GetDictValue(dataDict, "txn_type");
            ParentTxnId = GetDictValue(dataDict, "parent_txn_id");
            PaymentStatus = GetDictValue(dataDict, "payment_status");
            PaymentType = GetDictValue(dataDict, "payment_type");
            McCurrency = GetDictValue(dataDict, "mc_currency");
            Custom = GetDictValue(dataDict, "custom");
            ItemName = GetDictValue(dataDict, "item_name");
            ItemNumber = GetDictValue(dataDict, "item_number");
            PayerId = GetDictValue(dataDict, "payer_id");
            PayerEmail = GetDictValue(dataDict, "payer_email");
            ReceiverId = GetDictValue(dataDict, "receiver_id");
            ReceiverEmail = GetDictValue(dataDict, "receiver_email");

            PaymentDate = ConvertDate(GetDictValue(dataDict, "payment_date"));

            var mcGrossString = GetDictValue(dataDict, "mc_gross");
            if (!string.IsNullOrWhiteSpace(mcGrossString) && decimal.TryParse(mcGrossString, out var mcGross))
            {
                McGross = mcGross;
            }
            var mcFeeString = GetDictValue(dataDict, "mc_fee");
            if (!string.IsNullOrWhiteSpace(mcFeeString) && decimal.TryParse(mcFeeString, out var mcFee))
            {
                McFee = mcFee;
            }
        }

        public string TxnId { get; set; }
        public string TxnType { get; set; }
        public string ParentTxnId { get; set; }
        public string PaymentStatus { get; set; }
        public string PaymentType { get; set; }
        public decimal McGross { get; set; }
        public decimal McFee { get; set; }
        public string McCurrency { get; set; }
        public string Custom { get; set; }
        public string ItemName { get; set; }
        public string ItemNumber { get; set; }
        public string PayerId { get; set; }
        public string PayerEmail { get; set; }
        public string ReceiverId { get; set; }
        public string ReceiverEmail { get; set; }
        public DateTime PaymentDate { get; set; }

        public Tuple<Guid?, Guid?> GetIdsFromCustom()
        {
            Guid? orgId = null;
            Guid? userId = null;

            if (!string.IsNullOrWhiteSpace(Custom) && Custom.Contains(":"))
            {
                var mainParts = Custom.Split(',');
                foreach (var mainPart in mainParts)
                {
                    var parts = mainPart.Split(':');
                    if (parts.Length > 1 && Guid.TryParse(parts[1], out var id))
                    {
                        if (parts[0] == "user_id")
                        {
                            userId = id;
                        }
                        else if (parts[0] == "organization_id")
                        {
                            orgId = id;
                        }
                    }
                }
            }

            return new Tuple<Guid?, Guid?>(orgId, userId);
        }

        public bool IsAccountCredit()
        {
            return !string.IsNullOrWhiteSpace(Custom) && Custom.Contains("account_credit:1");
        }

        private string GetDictValue(IDictionary<string, string> dict, string key)
        {
            return dict.ContainsKey(key) ? dict[key] : null;
        }

        private DateTime ConvertDate(string dateString)
        {
            if (!string.IsNullOrWhiteSpace(dateString))
            {
                var parsed = DateTime.TryParseExact(dateString, _dateFormats,
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var paymentDate);
                if (parsed)
                {
                    var pacificTime = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ?
                        TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time") :
                        TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles");
                    return TimeZoneInfo.ConvertTimeToUtc(paymentDate, pacificTime);
                }
            }
            return default(DateTime);
        }
    }
}
