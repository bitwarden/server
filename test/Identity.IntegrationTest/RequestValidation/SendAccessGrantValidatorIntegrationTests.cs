using Bit.Core;
using Bit.Core.Enums;
using Bit.Core.IdentityServer;
using Bit.Core.Services;
using Bit.Core.Tools.Models.Data;
using Bit.Core.Tools.SendFeatures.Queries.Interfaces;
using Bit.Core.Utilities;
using Bit.Identity.IdentityServer.Enums;
using Bit.Identity.IdentityServer.RequestValidators.SendAccess;
using Bit.IntegrationTestCommon.Factories;
using Duende.IdentityServer.Validation;
using NSubstitute;
using Xunit;

namespace Bit.Identity.IntegrationTest.RequestValidation;

// in order to test the default case for the authentication method, we need to create a custom one so we can ensure the
// method throws as expected.
internal record AnUnknownAuthenticationMethod : SendAuthenticationMethod { }

public class SendAccessGrantValidatorIntegrationTests(IdentityApplicationFactory factory) : IClassFixture<IdentityApplicationFactory>
{
    private readonly IdentityApplicationFactory _factory = factory;

    [Fact]
    public async Task SendAccessGrant_FeatureFlagDisabled_ReturnsUnsupportedGrantType()
    {
        // Arrange
        var sendId = Guid.NewGuid();
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Mock feature service to return false
                var featureService = Substitute.For<IFeatureService>();
                featureService.IsEnabled(FeatureFlagKeys.SendAccess).Returns(false);
                services.AddSingleton(featureService);
            });
        }).CreateClient();

        var requestBody = CreateTokenRequestBody(sendId);

        // Act
        var response = await client.PostAsync("/connect/token", requestBody);

        // Assert
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("unsupported_grant_type", content);
    }

    [Fact]
    public async Task SendAccessGrant_ValidNotAuthenticatedSend_ReturnsAccessToken()
    {
        // Arrange
        var sendId = Guid.NewGuid();
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Mock feature service to return true
                var featureService = Substitute.For<IFeatureService>();
                featureService.IsEnabled(FeatureFlagKeys.SendAccess).Returns(true);
                services.AddSingleton(featureService);

                // Mock send authentication query
                var sendAuthQuery = Substitute.For<ISendAuthenticationQuery>();
                sendAuthQuery.GetAuthenticationMethod(sendId).Returns(new NotAuthenticated());
                services.AddSingleton(sendAuthQuery);
            });
        }).CreateClient();

        var requestBody = CreateTokenRequestBody(sendId);

        // Act
        var response = await client.PostAsync("/connect/token", requestBody);

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("access_token", content);
        Assert.Contains("bearer", content.ToLower());
    }

    [Fact]
    public async Task SendAccessGrant_MissingSendId_ReturnsInvalidRequest()
    {
        // Arrange
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var featureService = Substitute.For<IFeatureService>();
                featureService.IsEnabled(FeatureFlagKeys.SendAccess).Returns(true);
                services.AddSingleton(featureService);
            });
        }).CreateClient();

        var requestBody = new FormUrlEncodedContent([
            new KeyValuePair<string, string>("grant_type", CustomGrantTypes.SendAccess),
            new KeyValuePair<string, string>("client_id", BitwardenClient.Send)
        ]);

        // Act
        var response = await client.PostAsync("/connect/token", requestBody);

        // Assert
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("invalid_request", content);
        Assert.Contains("send_id is required", content);
    }

    [Fact]
    public async Task SendAccessGrant_NeverAuthenticateSend_ReturnsInvalidRequest()
    {
        // Arrange
        var sendId = Guid.NewGuid();
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var featureService = Substitute.For<IFeatureService>();
                featureService.IsEnabled(FeatureFlagKeys.SendAccess).Returns(true);
                services.AddSingleton(featureService);

                var sendAuthQuery = Substitute.For<ISendAuthenticationQuery>();
                sendAuthQuery.GetAuthenticationMethod(sendId).Returns(new NeverAuthenticate());
                services.AddSingleton(sendAuthQuery);
            });
        }).CreateClient();

        var requestBody = CreateTokenRequestBody(sendId);

        // Act
        var response = await client.PostAsync("/connect/token", requestBody);

        // Assert
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Invalid request", content);
    }

    [Fact]
    public async Task SendAccessGrant_UnknownAuthenticationMethod_ThrowsInvalidOperation()
    {
        // Arrange
        var sendId = Guid.NewGuid();
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var featureService = Substitute.For<IFeatureService>();
                featureService.IsEnabled(FeatureFlagKeys.SendAccess).Returns(true);
                services.AddSingleton(featureService);

                var sendAuthQuery = Substitute.For<ISendAuthenticationQuery>();
                sendAuthQuery.GetAuthenticationMethod(sendId).Returns(new AnUnknownAuthenticationMethod());
                services.AddSingleton(sendAuthQuery);
            });
        }).CreateClient();

        var requestBody = CreateTokenRequestBody(sendId);

        // Act
        var error = await client.PostAsync("/connect/token", requestBody);

        // Assert
        // We want to parse the response and ensure we get the correct error from the server
        var content = await error.Content.ReadAsStringAsync();
        Assert.Contains("invalid_grant", content);
    }

    [Fact]
    public async Task SendAccessGrant_PasswordProtectedSend_CallsPasswordValidator()
    {
        // Arrange
        var sendId = Guid.NewGuid();
        var resourcePassword = new ResourcePassword("test-password-hash");
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var featureService = Substitute.For<IFeatureService>();
                featureService.IsEnabled(FeatureFlagKeys.SendAccess).Returns(true);
                services.AddSingleton(featureService);

                var sendAuthQuery = Substitute.For<ISendAuthenticationQuery>();
                sendAuthQuery.GetAuthenticationMethod(sendId).Returns(resourcePassword);
                services.AddSingleton(sendAuthQuery);

                // Mock password validator to return success
                var passwordValidator = Substitute.For<ISendPasswordRequestValidator>();
                passwordValidator.ValidateSendPassword(
                    Arg.Any<ExtensionGrantValidationContext>(),
                    Arg.Any<ResourcePassword>(),
                    Arg.Any<Guid>())
                    .Returns(new GrantValidationResult(
                        subject: sendId.ToString(),
                        authenticationMethod: CustomGrantTypes.SendAccess));
                services.AddSingleton(passwordValidator);
            });
        }).CreateClient();

        var requestBody = CreateTokenRequestBody(sendId, "password123");

        // Act
        var response = await client.PostAsync("/connect/token", requestBody);

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("access_token", content);
        Assert.Contains("Bearer", content);
    }

    private static FormUrlEncodedContent CreateTokenRequestBody(
        Guid sendId,
        string password = null,
        string sendEmail = null,
        string emailOtp = null)
    {
        var sendIdBase64 = CoreHelpers.Base64UrlEncode(sendId.ToByteArray());
        var parameters = new List<KeyValuePair<string, string>>
        {
            new("grant_type", CustomGrantTypes.SendAccess),
            new("client_id", BitwardenClient.Send ),
            new("scope", ApiScopes.ApiSendAccess),
            new("deviceType", ((int)DeviceType.FirefoxBrowser).ToString()),
            new("send_id", sendIdBase64)
        };

        if (!string.IsNullOrEmpty(password))
        {
            parameters.Add(new("password_hash", password));
        }

        if (!string.IsNullOrEmpty(emailOtp) && !string.IsNullOrEmpty(sendEmail))
        {
            parameters.AddRange(
            [
                new KeyValuePair<string, string>("email", sendEmail),
                new KeyValuePair<string, string>("email_otp", emailOtp)
            ]);
        }

        return new FormUrlEncodedContent(parameters);
    }
}
