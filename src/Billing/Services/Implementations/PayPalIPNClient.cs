using System.Text;
using Microsoft.Extensions.Options;

namespace Bit.Billing.Services.Implementations;

public class PayPalIPNClient : IPayPalIPNClient
{
    private readonly HttpClient _httpClient;
    private readonly Uri _ipnEndpoint;
    private readonly ILogger<PayPalIPNClient> _logger;

    public PayPalIPNClient(
        IOptions<BillingSettings> billingSettings,
        HttpClient httpClient,
        ILogger<PayPalIPNClient> logger)
    {
        _httpClient = httpClient;
        // PayPal IPN postback verification must target the dedicated ipnpb host; the legacy www.paypal.com/cgi-bin/webscr
        // endpoint redirects and is not the documented postback target.
        _ipnEndpoint = new Uri(billingSettings.Value.PayPal.Production
            ? "https://ipnpb.paypal.com/cgi-bin/webscr"
            : "https://ipnpb.sandbox.paypal.com/cgi-bin/webscr");
        _logger = logger;
    }

    public async Task<PayPalIPNVerificationResult> VerifyIPN(string transactionId, string formData)
    {
        LogInfo(transactionId, $"Verifying IPN against {_ipnEndpoint}");

        if (string.IsNullOrEmpty(formData))
        {
            throw new ArgumentNullException(nameof(formData));
        }

        var requestMessage = new HttpRequestMessage { Method = HttpMethod.Post, RequestUri = _ipnEndpoint };

        var requestContent = string.Concat("cmd=_notify-validate&", formData);

        requestMessage.Content = new StringContent(requestContent, Encoding.UTF8, "application/x-www-form-urlencoded");

        HttpResponseMessage response;

        try
        {
            response = await _httpClient.SendAsync(requestMessage);
        }
        // Any failure to reach PayPal (network error, timeout, etc.) is transient and must not be treated as a forged
        // message: report it as Unverified so the caller can fail open instead of dropping a legitimate payment.
        catch (Exception exception)
        {
            LogError(transactionId, $"Verification request failed | {exception.Message}");
            return PayPalIPNVerificationResult.Unverified;
        }

        if (!response.IsSuccessStatusCode)
        {
            LogError(transactionId, $"Unsuccessful Response | Status Code: {response.StatusCode}");
            return PayPalIPNVerificationResult.Unverified;
        }

        var responseContent = await response.Content.ReadAsStringAsync();

        return responseContent switch
        {
            "VERIFIED" => Verified(),
            "INVALID" => Invalid(),
            _ => Unhandled(responseContent)
        };

        PayPalIPNVerificationResult Verified()
        {
            LogInfo(transactionId, "Verified");
            return PayPalIPNVerificationResult.Verified;
        }

        PayPalIPNVerificationResult Invalid()
        {
            LogError(transactionId, "Verification Invalid");
            return PayPalIPNVerificationResult.Invalid;
        }

        PayPalIPNVerificationResult Unhandled(string content)
        {
            LogWarning(transactionId, $"Unhandled Response Content: {content}");
            return PayPalIPNVerificationResult.Unverified;
        }
    }

    private void LogInfo(string transactionId, string message)
        => _logger.LogInformation("Verify PayPal IPN ({Id}) | {Message}", transactionId, message);

    private void LogWarning(string transactionId, string message)
        => _logger.LogWarning("Verify PayPal IPN ({Id}) | {Message}", transactionId, message);

    private void LogError(string transactionId, string message)
        => _logger.LogError("Verify PayPal IPN ({Id}) | {Message}", transactionId, message);
}
