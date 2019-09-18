using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Bit.Billing.Models;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bit.Core.Services.Implementations
{
    public class AppleIapService : IAppleIapService
    {
        private readonly HttpClient _httpClient = new HttpClient();

        private readonly GlobalSettings _globalSettings;
        private readonly IHostingEnvironment _hostingEnvironment;
        private readonly IMetaDataRespository _metaDataRespository;
        private readonly ILogger<AppleIapService> _logger;

        public AppleIapService(
            GlobalSettings globalSettings,
            IHostingEnvironment hostingEnvironment,
            IMetaDataRespository metaDataRespository,
            ILogger<AppleIapService> logger)
        {
            _globalSettings = globalSettings;
            _hostingEnvironment = hostingEnvironment;
            _metaDataRespository = metaDataRespository;
            _logger = logger;
        }

        public async Task<AppleReceiptStatus> GetVerifiedReceiptStatusAsync(string receiptData)
        {
            var receiptStatus = await GetReceiptStatusAsync(receiptData);
            if(receiptStatus?.Status != 0)
            {
                return null;
            }
            var validEnvironment = (!_hostingEnvironment.IsProduction() && receiptStatus.Environment == "Sandbox") ||
                (_hostingEnvironment.IsProduction() && receiptStatus.Environment != "Sandbox");
            var validProductBundle = receiptStatus.Receipt.BundleId == "com.bitwarden.desktop" ||
                receiptStatus.Receipt.BundleId == "com.8bit.bitwarden";
            var validProduct = receiptStatus.LatestReceiptInfo.LastOrDefault()?.ProductId == "premium_annually";
            if(validEnvironment && validProductBundle && validProduct &&
                receiptStatus.GetOriginalTransactionId() != null &&
                receiptStatus.GetLastTransactionId() != null)
            {
                return receiptStatus;
            }
            return null;
        }

        public async Task SaveReceiptAsync(AppleReceiptStatus receiptStatus)
        {
            var originalTransactionId = receiptStatus.GetOriginalTransactionId();
            if(string.IsNullOrWhiteSpace(originalTransactionId))
            {
                throw new Exception("OriginalTransactionId is null");
            }
            await _metaDataRespository.UpsertAsync("AppleReceipt", originalTransactionId,
                new Dictionary<string, string>
                {
                    ["Data"] = receiptStatus.GetReceiptData(),
                    ["UserId"] = receiptStatus.GetReceiptData()
                });
        }

        public async Task<Tuple<string, Guid?>> GetReceiptAsync(string originalTransactionId)
        {
            var receipt = await _metaDataRespository.GetAsync("AppleReceipt", originalTransactionId);
            if(receipt == null)
            {
                return null;
            }
            return new Tuple<string, Guid?>(receipt.ContainsKey("Data") ? receipt["Data"] : null,
                receipt.ContainsKey("UserId") ? new Guid(receipt["UserId"]) : (Guid?)null);
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
