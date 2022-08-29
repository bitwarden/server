using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Bit.Billing.Models;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Services;

public class AppleIapService : IAppleIapService
{
    private readonly HttpClient _httpClient = new HttpClient();

    private readonly GlobalSettings _globalSettings;
    private readonly IWebHostEnvironment _hostingEnvironment;
    private readonly IMetaDataRepository _metaDataRespository;
    private readonly ILogger<AppleIapService> _logger;

    public AppleIapService(
        GlobalSettings globalSettings,
        IWebHostEnvironment hostingEnvironment,
        IMetaDataRepository metaDataRespository,
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
        if (receiptStatus?.Status != 0)
        {
            return null;
        }
        var validEnvironment = _globalSettings.AppleIap.AppInReview ||
            (!(_hostingEnvironment.IsProduction() || _hostingEnvironment.IsEnvironment("QA")) && receiptStatus.Environment == "Sandbox") ||
            ((_hostingEnvironment.IsProduction() || _hostingEnvironment.IsEnvironment("QA")) && receiptStatus.Environment != "Sandbox");
        var validProductBundle = receiptStatus.Receipt.BundleId == "com.bitwarden.desktop" ||
            receiptStatus.Receipt.BundleId == "com.8bit.bitwarden";
        var validProduct = receiptStatus.LatestReceiptInfo.LastOrDefault()?.ProductId == "premium_annually";
        var validIds = receiptStatus.GetOriginalTransactionId() != null &&
            receiptStatus.GetLastTransactionId() != null;
        var validTransaction = receiptStatus.GetLastExpiresDate()
            .GetValueOrDefault(DateTime.MinValue) > DateTime.UtcNow;
        if (validEnvironment && validProductBundle && validProduct && validIds && validTransaction)
        {
            return receiptStatus;
        }
        return null;
    }

    public async Task SaveReceiptAsync(AppleReceiptStatus receiptStatus, Guid userId)
    {
        var originalTransactionId = receiptStatus.GetOriginalTransactionId();
        if (string.IsNullOrWhiteSpace(originalTransactionId))
        {
            throw new Exception("OriginalTransactionId is null");
        }
        await _metaDataRespository.UpsertAsync("AppleReceipt", originalTransactionId,
            new Dictionary<string, string>
            {
                ["Data"] = receiptStatus.GetReceiptData(),
                ["UserId"] = userId.ToString()
            });
    }

    public async Task<Tuple<string, Guid?>> GetReceiptAsync(string originalTransactionId)
    {
        var receipt = await _metaDataRespository.GetAsync("AppleReceipt", originalTransactionId);
        if (receipt == null)
        {
            return null;
        }
        return new Tuple<string, Guid?>(receipt.ContainsKey("Data") ? receipt["Data"] : null,
            receipt.ContainsKey("UserId") ? new Guid(receipt["UserId"]) : (Guid?)null);
    }

    // Internal for testing
    internal async Task<AppleReceiptStatus> GetReceiptStatusAsync(string receiptData, bool prod = true,
        int attempt = 0, AppleReceiptStatus lastReceiptStatus = null)
    {
        try
        {
            if (attempt > 4)
            {
                throw new Exception(
                    $"Failed verifying Apple IAP after too many attempts. Last attempt status: {lastReceiptStatus?.Status.ToString() ?? "null"}");
            }

            var url = string.Format("https://{0}.itunes.apple.com/verifyReceipt", prod ? "buy" : "sandbox");

            var response = await _httpClient.PostAsJsonAsync(url, new AppleVerifyReceiptRequestModel
            {
                ReceiptData = receiptData,
                Password = _globalSettings.AppleIap.Password
            });

            if (response.IsSuccessStatusCode)
            {
                var receiptStatus = await response.Content.ReadFromJsonAsync<AppleReceiptStatus>();
                if (receiptStatus.Status == 21007)
                {
                    return await GetReceiptStatusAsync(receiptData, false, attempt + 1, receiptStatus);
                }
                else if (receiptStatus.Status == 21005)
                {
                    await Task.Delay(2000);
                    return await GetReceiptStatusAsync(receiptData, prod, attempt + 1, receiptStatus);
                }
                return receiptStatus;
            }
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Error verifying Apple IAP receipt.");
        }
        return null;
    }
}

public class AppleVerifyReceiptRequestModel
{
    [JsonPropertyName("receipt-data")]
    public string ReceiptData { get; set; }
    [JsonPropertyName("password")]
    public string Password { get; set; }
}
