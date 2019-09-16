using System;
using System.Net.Http;
using System.Threading.Tasks;
using Bit.Billing.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bit.Core.Services.Implementations
{
    public class AppleIapService : IAppleIapService
    {
        private readonly HttpClient _httpClient = new HttpClient();

        private readonly GlobalSettings _globalSettings;
        private readonly ILogger<AppleIapService> _logger;

        public AppleIapService(
            GlobalSettings globalSettings,
            ILogger<AppleIapService> logger)
        {
            _globalSettings = globalSettings;
            _logger = logger;
        }

        public async Task<bool> VerifyReceiptAsync(string receiptData)
        {
            var receiptStatus = await GetReceiptStatusAsync(receiptData);
            return receiptStatus?.Status == 0;
        }

        private async Task<AppleReceiptStatus> GetReceiptStatusAsync(string receiptData, bool prod = true,
            int attempt = 0, AppleReceiptStatus lastReceiptStatus = null)
        {
            try
            {
                if(attempt > 4)
                {
                    throw new Exception("Failed verifying Apple IAP after too many attempts. Last attempt status: " +
                        lastReceiptStatus?.Status ?? "null");
                }

                var url = string.Format("https://{0}.itunes.apple.com/verifyReceipt", prod ? "buy" : "sandbox");
                var json = new JObject(new JProperty("receipt-data", receiptData),
                   new JProperty("password", _globalSettings.AppleIapPassword)).ToString();

                var response = await _httpClient.PostAsync(url, new StringContent(json));
                if(response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    var receiptStatus = JsonConvert.DeserializeObject<AppleReceiptStatus>(responseJson);
                    if(receiptStatus.Status == 21007)
                    {
                        return await GetReceiptStatusAsync(receiptData, false, attempt + 1, receiptStatus);
                    }
                    else if(receiptStatus.Status == 21005)
                    {
                        await Task.Delay(2000);
                        return await GetReceiptStatusAsync(receiptData, prod, attempt + 1, receiptStatus);
                    }
                    return receiptStatus;
                }
            }
            catch(Exception e)
            {
                _logger.LogWarning(e, "Error verifying Apple IAP receipt.");
            }
            return null;
        }
    }
}
