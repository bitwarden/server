using System.Net;
using Bit.Billing.Services;
using Bit.Billing.Services.Implementations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using RichardSzalay.MockHttp;
using Xunit;

namespace Bit.Billing.Test.Services;

public class PayPalIPNClientTests
{
    private readonly Uri _endpoint = new("https://ipnpb.sandbox.paypal.com/cgi-bin/webscr");
    private readonly MockHttpMessageHandler _mockHttpMessageHandler = new();

    private readonly IOptions<BillingSettings> _billingSettings = Substitute.For<IOptions<BillingSettings>>();
    private readonly ILogger<PayPalIPNClient> _logger = Substitute.For<ILogger<PayPalIPNClient>>();

    private readonly IPayPalIPNClient _payPalIPNClient;

    public PayPalIPNClientTests()
    {
        var httpClient = new HttpClient(_mockHttpMessageHandler)
        {
            BaseAddress = _endpoint
        };

        _payPalIPNClient = new PayPalIPNClient(
            _billingSettings,
            httpClient,
            _logger);
    }

    [Fact]
    public async Task VerifyIPN_FormDataNull_ThrowsArgumentNullException()
        => await Assert.ThrowsAsync<ArgumentNullException>(() => _payPalIPNClient.VerifyIPN(string.Empty, null));

    [Fact]
    public async Task VerifyIPN_Unauthorized_ReturnsUnverified()
    {
        const string formData = "form=data";

        var request = _mockHttpMessageHandler
            .Expect(HttpMethod.Post, _endpoint.ToString())
            .WithFormData(new Dictionary<string, string> { { "cmd", "_notify-validate" }, { "form", "data" } })
            .Respond(HttpStatusCode.Unauthorized);

        var result = await _payPalIPNClient.VerifyIPN(string.Empty, formData);

        Assert.Equal(PayPalIPNVerificationResult.Unverified, result);
        Assert.Equal(1, _mockHttpMessageHandler.GetMatchCount(request));
    }

    [Fact]
    public async Task VerifyIPN_OK_Invalid_ReturnsInvalid()
    {
        const string formData = "form=data";

        var request = _mockHttpMessageHandler
            .Expect(HttpMethod.Post, _endpoint.ToString())
            .WithFormData(new Dictionary<string, string> { { "cmd", "_notify-validate" }, { "form", "data" } })
            .Respond("application/text", "INVALID");

        var result = await _payPalIPNClient.VerifyIPN(string.Empty, formData);

        Assert.Equal(PayPalIPNVerificationResult.Invalid, result);
        Assert.Equal(1, _mockHttpMessageHandler.GetMatchCount(request));
    }

    [Fact]
    public async Task VerifyIPN_OK_Verified_ReturnsVerified()
    {
        const string formData = "form=data";

        var request = _mockHttpMessageHandler
            .Expect(HttpMethod.Post, _endpoint.ToString())
            .WithFormData(new Dictionary<string, string> { { "cmd", "_notify-validate" }, { "form", "data" } })
            .Respond("application/text", "VERIFIED");

        var result = await _payPalIPNClient.VerifyIPN(string.Empty, formData);

        Assert.Equal(PayPalIPNVerificationResult.Verified, result);
        Assert.Equal(1, _mockHttpMessageHandler.GetMatchCount(request));
    }

    [Fact]
    public async Task VerifyIPN_RequestThrows_ReturnsUnverified()
    {
        const string formData = "form=data";

        _mockHttpMessageHandler
            .When(HttpMethod.Post, _endpoint.ToString())
            .Throw(new HttpRequestException("Simulated network failure"));

        var result = await _payPalIPNClient.VerifyIPN(string.Empty, formData);

        Assert.Equal(PayPalIPNVerificationResult.Unverified, result);
    }
}
