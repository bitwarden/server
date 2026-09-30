using System.Net;
using System.Text.Json;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Services;
using Bit.Core.Tools.Models.Data;
using Bit.Core.Tools.SendFeatures.Queries.Interfaces;
using Bit.Identity.IdentityServer.RequestValidators.SendAccess;
using Bit.IntegrationTestCommon.Factories;
using Duende.IdentityModel;
using NSubstitute;
using Xunit;

namespace Bit.Identity.IntegrationTest.RequestValidation.SendAccess;

/// <summary>
/// Drives the Send access email code flow through real <c>/connect/token</c> requests, with the real token
/// provider and cache. 
/// </summary>
public class SendEmailOtpDeviceIdentifierIntegrationTests(IdentityApplicationFactory _factory)
    : IClassFixture<IdentityApplicationFactory>
{
    /// <summary>The device that requests the code.</summary>
    private const string RequestingDeviceIdentifier = "requesting-device-identifier";

    /// <summary>A second device, used to submit a code it did not request.</summary>
    private const string OtherDeviceIdentifier = "other-device-identifier";

    [Fact]
    public async Task SendAccess_EmailOtp_CodeFromSameDevice_ReturnsAccessToken()
    {
        var (client, sendId, email) = ArrangeEmailOtpSend();

        await PostSendAccessTokenAsync(client, sendId, email, deviceIdentifier: RequestingDeviceIdentifier);
        var code = _factory.SendAccessEmailOtpCodes[email];

        var response = await PostSendAccessTokenAsync(
            client, sendId, email, code, RequestingDeviceIdentifier);

        await AssertAccessTokenIssuedAsync(response);
    }

    [Fact]
    public async Task SendAccess_EmailOtp_CodeFromDifferentDevice_IsRejected()
    {
        var (client, sendId, email) = ArrangeEmailOtpSend();

        await PostSendAccessTokenAsync(client, sendId, email, deviceIdentifier: RequestingDeviceIdentifier);
        var code = _factory.SendAccessEmailOtpCodes[email];

        var response = await PostSendAccessTokenAsync(client, sendId, email, code, OtherDeviceIdentifier);

        await AssertRejectedAsync(response, SendAccessConstants.EmailOtpValidatorResults.EmailAndOtpRequired);
    }

    [Fact]
    public async Task SendAccess_EmailOtp_CodeRequestedWithDevice_RedeemedWithoutDevice_IsRejected()
    {
        var (client, sendId, email) = ArrangeEmailOtpSend();

        await PostSendAccessTokenAsync(client, sendId, email, deviceIdentifier: RequestingDeviceIdentifier);
        var code = _factory.SendAccessEmailOtpCodes[email];

        var response = await PostSendAccessTokenAsync(client, sendId, email, code, deviceIdentifier: null);

        await AssertRejectedAsync(response, SendAccessConstants.EmailOtpValidatorResults.DeviceIdentifierRequired);
    }

    [Fact]
    public async Task SendAccess_EmailOtp_RequestFromAnotherDevice_DoesNotInvalidateFirstDevicesCode()
    {
        var (client, sendId, email) = ArrangeEmailOtpSend();

        await PostSendAccessTokenAsync(client, sendId, email, deviceIdentifier: RequestingDeviceIdentifier);
        var firstCode = _factory.SendAccessEmailOtpCodes[email];

        await PostSendAccessTokenAsync(client, sendId, email, deviceIdentifier: OtherDeviceIdentifier);
        var secondCode = _factory.SendAccessEmailOtpCodes[email];

        var firstCodeResponse = await PostSendAccessTokenAsync(
            client, sendId, email, firstCode, RequestingDeviceIdentifier);
        await AssertAccessTokenIssuedAsync(firstCodeResponse);

        var secondCodeResponse = await PostSendAccessTokenAsync(
            client, sendId, email, secondCode, OtherDeviceIdentifier);
        await AssertAccessTokenIssuedAsync(secondCodeResponse);
    }

    [Fact]
    public async Task SendAccess_EmailOtp_NewRequestFromSameDevice_SupersedesPriorCode()
    {
        var (client, sendId, email) = ArrangeEmailOtpSend();

        await PostSendAccessTokenAsync(client, sendId, email, deviceIdentifier: RequestingDeviceIdentifier);
        var firstCode = _factory.SendAccessEmailOtpCodes[email];

        await PostSendAccessTokenAsync(client, sendId, email, deviceIdentifier: RequestingDeviceIdentifier);
        var secondCode = _factory.SendAccessEmailOtpCodes[email];

        var firstCodeResponse = await PostSendAccessTokenAsync(
            client, sendId, email, firstCode, RequestingDeviceIdentifier);
        await AssertRejectedAsync(firstCodeResponse, SendAccessConstants.EmailOtpValidatorResults.EmailAndOtpRequired);

        // The later code still works, so the rejection above is not a blanket failure.
        var secondCodeResponse = await PostSendAccessTokenAsync(
            client, sendId, email, secondCode, RequestingDeviceIdentifier);
        await AssertAccessTokenIssuedAsync(secondCodeResponse);
    }

    [Fact]
    public async Task SendAccess_EmailOtp_RequestWithoutDevice_ReturnsDeviceIdentifierRequired_SendsNoEmail()
    {
        var (client, sendId, email) = ArrangeEmailOtpSend();

        var response = await PostSendAccessTokenAsync(client, sendId, email, deviceIdentifier: null);

        await AssertRejectedAsync(response, SendAccessConstants.EmailOtpValidatorResults.DeviceIdentifierRequired);
        Assert.False(_factory.SendAccessEmailOtpCodes.ContainsKey(email));
    }

    [Fact]
    public async Task SendAccess_EmailOtp_OverLongDevice_ReturnsDeviceIdentifierInvalid_SendsNoEmail()
    {
        var (client, sendId, email) = ArrangeEmailOtpSend();
        var overLongDeviceIdentifier = new string('a', Device.MaxIdentifierLength + 1);

        var response = await PostSendAccessTokenAsync(
            client, sendId, email, deviceIdentifier: overLongDeviceIdentifier);

        await AssertRejectedAsync(response, SendAccessConstants.EmailOtpValidatorResults.DeviceIdentifierInvalid);
        Assert.False(_factory.SendAccessEmailOtpCodes.ContainsKey(email));
    }

    /// <summary>
    /// Arranges an email-protected Send with a unique Send id and email per test, so captured codes from
    /// other tests sharing the fixture never collide.
    /// </summary>
    private (HttpClient client, Guid sendId, string email) ArrangeEmailOtpSend()
    {
        var sendId = Guid.NewGuid();
        var email = $"{Guid.NewGuid()}@example.com";

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var featureService = Substitute.For<IFeatureService>();
                featureService.IsEnabled(Arg.Any<string>()).Returns(true);
                services.AddSingleton(featureService);

                var sendAuthQuery = Substitute.For<ISendAuthenticationQuery>();
                sendAuthQuery.GetAuthenticationMethod(sendId).Returns(new EmailOtp([email]));
                services.AddSingleton(sendAuthQuery);

                // IOtpTokenProvider is deliberately not substituted: the device binding lives in the real
                // provider's cache entry. IMailService stays the factory's substitute, which captures codes.
            });
        }).CreateClient();

        return (client, sendId, email);
    }

    private static async Task<HttpResponseMessage> PostSendAccessTokenAsync(
        HttpClient client,
        Guid sendId,
        string email,
        string? otp = null,
        string? deviceIdentifier = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/connect/token");
        request.Content = SendAccessTestUtilities.CreateTokenRequestBody(sendId, email: email, emailOtp: otp);
        if (deviceIdentifier != null)
        {
            request.Headers.Add(RequestHeaderNames.DeviceIdentifier, deviceIdentifier);
        }

        return await client.SendAsync(request);
    }

    private static async Task AssertAccessTokenIssuedAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, content);
        Assert.Contains(OidcConstants.TokenResponse.AccessToken, content);
    }

    private static async Task AssertRejectedAsync(HttpResponseMessage response, string expectedSendAccessError)
    {
        var content = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var body = JsonDocument.Parse(content);
        Assert.Equal(OidcConstants.TokenErrors.InvalidRequest, body.RootElement.GetProperty("error").GetString());
        Assert.Equal(
            expectedSendAccessError,
            body.RootElement.GetProperty(SendAccessConstants.SendAccessError).GetString());
        Assert.False(body.RootElement.TryGetProperty(OidcConstants.TokenResponse.AccessToken, out _));
    }
}
