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

        public async Task<bool> VerifyReceiptAsync(string receiptData)
        {
            var receiptStatus = await GetVerifiedReceiptStatusAsync(receiptData);
            return receiptStatus != null;
        }

        public async Task<string> GetVerifiedLastTransactionIdAsync(string receiptData)
        {
            var receiptStatus = await GetVerifiedReceiptStatusAsync(receiptData);
            return receiptStatus?.LatestReceiptInfo?.LastOrDefault()?.TransactionId;
        }

        public async Task<DateTime?> GetVerifiedLastExpiresDateAsync(string receiptData)
        {
            var receiptStatus = await GetVerifiedReceiptStatusAsync(receiptData);
            return receiptStatus?.LatestReceiptInfo?.LastOrDefault()?.ExpiresDate;
        }

        public string HashReceipt(string receiptData)
        {
            using(var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(Convert.FromBase64String(receiptData));
                return BitConverter.ToString(hash).Replace("-", string.Empty);
            }
        }

        public async Task SaveReceiptAsync(string receiptData)
        {
            var hash = HashReceipt(receiptData);
            await _metaDataRespository.UpsertAsync("appleReceipt", hash,
                new KeyValuePair<string, string>("data", receiptData));
        }

        public async Task<string> GetReceiptAsync(string hash)
        {
            var receipt = await _metaDataRespository.GetAsync("appleReceipt", hash);
            return receipt != null && receipt.ContainsKey("data") ? receipt["data"] : null;
        }

        private async Task<AppleReceiptStatus> GetVerifiedReceiptStatusAsync(string receiptData)
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
            if(validEnvironment && validProductBundle && validProduct)
            {
                return receiptStatus;
            }
            return null;
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
