using Bit.Core.Auth.Identity.TokenProviders;
using Bit.Core.Enums;
using Bit.Core.IdentityServer;
using Bit.Core.Services;
using Bit.Core.Tools.Models.Data;
using Bit.Core.Tools.SendFeatures.Queries.Interfaces;
using Bit.Core.Utilities;
using Bit.Identity.IdentityServer.Enums;
using Bit.Identity.IdentityServer.RequestValidators.SendAccess;
using Bit.IntegrationTestCommon.Factories;
using Duende.IdentityModel;
using NSubstitute;
using Xunit;

namespace Bit.Identity.IntegrationTest.RequestValidation;

public class SendEmailOtpRequestValidatorIntegrationTests : IClassFixture<IdentityApplicationFactory>
{
    private readonly IdentityApplicationFactory _factory;

    public SendEmailOtpRequestValidatorIntegrationTests(IdentityApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SendAccess_EmailOtpProtectedSend_MissingEmail_ReturnsInvalidRequest()
    {
        // Arrange
        var sendId = Guid.NewGuid();
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var featureService = Substitute.For<IFeatureService>();
                featureService.IsEnabled(Arg.Any<string>()).Returns(true);
                services.AddSingleton(featureService);

                var sendAuthQuery = Substitute.For<ISendAuthenticationQuery>();
                sendAuthQuery.GetAuthenticationMethod(sendId)
                    .Returns(new EmailOtp(["test@example.com"]));
                services.AddSingleton(sendAuthQuery);
            });
        }).CreateClient();

        var requestBody = CreateTokenRequestBody(sendId); // No email

        // Act
        var response = await client.PostAsync("/connect/token", requestBody);

        // Assert
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains(OidcConstants.TokenErrors.InvalidGrant, content);
        Assert.Contains("email is required", content);
    }

    [Fact]
    public async Task SendAccess_EmailOtpProtectedSend_EmailWithoutOtp_SendsOtpEmail()
    {
        // Arrange
        var sendId = Guid.NewGuid();
        var email = "test@example.com";
        var generatedToken = "123456";

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var featureService = Substitute.For<IFeatureService>();
                featureService.IsEnabled(Arg.Any<string>()).Returns(true);
                services.AddSingleton(featureService);

                var sendAuthQuery = Substitute.For<ISendAuthenticationQuery>();
                sendAuthQuery.GetAuthenticationMethod(sendId)
                    .Returns(new EmailOtp([email]));
                services.AddSingleton(sendAuthQuery);

                // Mock OTP token provider
                var otpProvider = Substitute.For<IOtpTokenProvider<DefaultOtpTokenProviderOptions>>();
                otpProvider.GenerateTokenAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
                    .Returns(generatedToken);
                services.AddSingleton(otpProvider);

                // Mock mail service
                var mailService = Substitute.For<IMailService>();
                services.AddSingleton(mailService);
            });
        }).CreateClient();

        var requestBody = CreateTokenRequestBody(sendId, sendEmail: email); // Email but no OTP

        // Act
        var response = await client.PostAsync("/connect/token", requestBody);

        // Assert
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains(OidcConstants.TokenErrors.InvalidRequest, content);
        Assert.Contains("email otp sent", content);
    }

    [Fact]
    public async Task SendAccess_EmailOtpProtectedSend_ValidOtp_ReturnsAccessToken()
    {
        // Arrange
        var sendId = Guid.NewGuid();
        var email = "test@example.com";
        var otp = "123456";

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var featureService = Substitute.For<IFeatureService>();
                featureService.IsEnabled(Arg.Any<string>()).Returns(true);
                services.AddSingleton(featureService);

                var sendAuthQuery = Substitute.For<ISendAuthenticationQuery>();
                sendAuthQuery.GetAuthenticationMethod(sendId)
                    .Returns(new EmailOtp(new[] { email }));
                services.AddSingleton(sendAuthQuery);

                // Mock OTP token provider to validate successfully
                var otpProvider = Substitute.For<IOtpTokenProvider<DefaultOtpTokenProviderOptions>>();
                otpProvider.ValidateTokenAsync(otp, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
                    .Returns(true);
                services.AddSingleton(otpProvider);

                var mailService = Substitute.For<IMailService>();
                services.AddSingleton(mailService);
            });
        }).CreateClient();

        var requestBody = CreateTokenRequestBody(sendId, sendEmail: email, emailOtp: otp);

        // Act
        var response = await client.PostAsync("/connect/token", requestBody);

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains(OidcConstants.TokenResponse.AccessToken, content);
        Assert.Contains(OidcConstants.TokenResponse.BearerTokenType, content.ToLower());
    }

    [Fact]
    public async Task SendAccess_EmailOtpProtectedSend_InvalidOtp_ReturnsInvalidGrant()
    {
        // Arrange
        var sendId = Guid.NewGuid();
        var email = "test@example.com";
        var invalidOtp = "wrong123";

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var featureService = Substitute.For<IFeatureService>();
                featureService.IsEnabled(Arg.Any<string>()).Returns(true);
                services.AddSingleton(featureService);

                var sendAuthQuery = Substitute.For<ISendAuthenticationQuery>();
                sendAuthQuery.GetAuthenticationMethod(sendId)
                    .Returns(new EmailOtp(new[] { email }));
                services.AddSingleton(sendAuthQuery);

                // Mock OTP token provider to validate as false
                var otpProvider = Substitute.For<IOtpTokenProvider<DefaultOtpTokenProviderOptions>>();
                otpProvider.ValidateTokenAsync(invalidOtp, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
                    .Returns(false);
                services.AddSingleton(otpProvider);

                var mailService = Substitute.For<IMailService>();
                services.AddSingleton(mailService);
            });
        }).CreateClient();

        var requestBody = CreateTokenRequestBody(sendId, sendEmail: email, emailOtp: invalidOtp);

        // Act
        var response = await client.PostAsync("/connect/token", requestBody);

        // Assert
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains(OidcConstants.TokenErrors.InvalidGrant, content);
        Assert.Contains("email otp is invalid", content);
    }

    [Fact]
    public async Task SendAccess_EmailOtpProtectedSend_OtpGenerationFails_ReturnsInvalidRequest()
    {
        // Arrange
        var sendId = Guid.NewGuid();
        var email = "test@example.com";

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var featureService = Substitute.For<IFeatureService>();
                featureService.IsEnabled(Arg.Any<string>()).Returns(true);
                services.AddSingleton(featureService);

                var sendAuthQuery = Substitute.For<ISendAuthenticationQuery>();
                sendAuthQuery.GetAuthenticationMethod(sendId)
                    .Returns(new EmailOtp(new[] { email }));
                services.AddSingleton(sendAuthQuery);

                // Mock OTP token provider to fail generation
                var otpProvider = Substitute.For<IOtpTokenProvider<DefaultOtpTokenProviderOptions>>();
                otpProvider.GenerateTokenAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
                    .Returns((string)null);
                services.AddSingleton(otpProvider);

                var mailService = Substitute.For<IMailService>();
                services.AddSingleton(mailService);
            });
        }).CreateClient();

        var requestBody = CreateTokenRequestBody(sendId, sendEmail: email); // Email but no OTP

        // Act
        var response = await client.PostAsync("/connect/token", requestBody);

        // Assert
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains(OidcConstants.TokenErrors.InvalidRequest, content);
    }

    private static FormUrlEncodedContent CreateTokenRequestBody(Guid sendId,
    string sendEmail = null, string emailOtp = null)
    {
        var sendIdBase64 = CoreHelpers.Base64UrlEncode(sendId.ToByteArray());
        var parameters = new List<KeyValuePair<string, string>>
        {
            new(OidcConstants.TokenRequest.GrantType, CustomGrantTypes.SendAccess),
            new(OidcConstants.TokenRequest.ClientId, BitwardenClient.Send ),
            new(OidcConstants.TokenRequest.Scope, ApiScopes.ApiSendAccess),
            new("deviceType", ((int)DeviceType.FirefoxBrowser).ToString()),
            new(SendAccessConstants.TokenRequest.SendId, sendIdBase64)
        };

        if (!string.IsNullOrEmpty(sendEmail))
        {
            parameters.Add(new KeyValuePair<string, string>(
                SendAccessConstants.TokenRequest.Email, sendEmail));
        }

        if (!string.IsNullOrEmpty(emailOtp))
        {
            parameters.Add(new KeyValuePair<string, string>(
                SendAccessConstants.TokenRequest.Otp, emailOtp));
        }

        return new FormUrlEncodedContent(parameters);
    }
}
