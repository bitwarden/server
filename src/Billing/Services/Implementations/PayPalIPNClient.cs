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
        _ipnEndpoint = new Uri(billingSettings.Value.PayPal.Production
            ? "https://www.paypal.com/cgi-bin/webscr"
            : "https://www.sandbox.paypal.com/cgi-bin/webscr");
        _logger = logger;
    }

    public async Task<bool> VerifyIPN(Guid entityId, string formData)
    {
        LogInfo(entityId, $"Verifying IPN against {_ipnEndpoint}");

        if (string.IsNullOrEmpty(formData))
        {
            throw new ArgumentNullException(nameof(formData));
        }

        var requestMessage = new HttpRequestMessage { Method = HttpMethod.Post, RequestUri = _ipnEndpoint };

        var requestContent = string.Concat("cmd=_notify-validate&", formData);

        LogInfo(entityId, $"Request Content: {requestContent}");

        requestMessage.Content = new StringContent(requestContent, Encoding.UTF8, "application/x-www-form-urlencoded");

        var response = await _httpClient.SendAsync(requestMessage);

        var responseContent = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            return responseContent switch
            {
                "VERIFIED" => Verified(),
                "INVALID" => Invalid(),
                _ => Unhandled(responseContent)
            };
        }

        LogError(entityId, $"Unsuccessful Response | Status Code: {response.StatusCode} | Content: {responseContent}");

        return false;

        bool Verified()
        {
            LogInfo(entityId, "Verified");
            return true;
        }

        bool Invalid()
        {
            LogError(entityId, "Verification Invalid");
            return false;
        }

        bool Unhandled(string content)
        {
            LogWarning(entityId, $"Unhandled Response Content: {content}");
            return false;
        }
    }

    private void LogInfo(Guid entityId, string message)
        => _logger.LogInformation("Verify PayPal IPN ({RequestId}) | {Message}", entityId, message);

    private void LogWarning(Guid entityId, string message)
        => _logger.LogWarning("Verify PayPal IPN ({RequestId}) | {Message}", entityId, message);

    private void LogError(Guid entityId, string message)
        => _logger.LogError("Verify PayPal IPN ({RequestId}) | {Message}", entityId, message);
}
