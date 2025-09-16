using Bit.Core.Auth.IdentityServer;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Sends;
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

namespace Bit.Identity.IntegrationTest.RequestValidation.SendAccess;

public class SendPasswordRequestValidatorIntegrationTests : IClassFixture<IdentityApplicationFactory>
{
    private readonly IdentityApplicationFactory _factory;

    public SendPasswordRequestValidatorIntegrationTests(IdentityApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SendAccess_PasswordProtectedSend_ValidPassword_ReturnsAccessToken()
    {
        // Arrange
        var sendId = Guid.NewGuid();
        var passwordHash = "stored-password-hash";
        var clientPasswordHash = "client-password-hash";

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Enable feature flag
                var featureService = Substitute.For<IFeatureService>();
                featureService.IsEnabled(Arg.Any<string>()).Returns(true);
                services.AddSingleton(featureService);

                // Mock send authentication query
                var sendAuthQuery = Substitute.For<ISendAuthenticationQuery>();
                sendAuthQuery.GetAuthenticationMethod(sendId)
                    .Returns(new ResourcePassword(passwordHash));
                services.AddSingleton(sendAuthQuery);

                // Mock password hasher to return true for matching passwords
                var passwordHasher = Substitute.For<ISendPasswordHasher>();
                passwordHasher.PasswordHashMatches(passwordHash, clientPasswordHash)
                    .Returns(true);
                services.AddSingleton(passwordHasher);
            });
        }).CreateClient();

        var requestBody = CreateTokenRequestBody(sendId, clientPasswordHash);

        // Act
        var response = await client.PostAsync("/connect/token", requestBody);

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains(OidcConstants.TokenResponse.AccessToken, content);
        Assert.Contains("bearer", content.ToLower());
    }

    [Fact]
    public async Task SendAccess_PasswordProtectedSend_InvalidPassword_ReturnsInvalidGrant()
    {
        // Arrange
        var sendId = Guid.NewGuid();
        var passwordHash = "stored-password-hash";
        var wrongClientPasswordHash = "wrong-client-password-hash";

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var featureService = Substitute.For<IFeatureService>();
                featureService.IsEnabled(Arg.Any<string>()).Returns(true);
                services.AddSingleton(featureService);

                var sendAuthQuery = Substitute.For<ISendAuthenticationQuery>();
                sendAuthQuery.GetAuthenticationMethod(sendId)
                    .Returns(new ResourcePassword(passwordHash));
                services.AddSingleton(sendAuthQuery);

                // Mock password hasher to return false for wrong passwords
                var passwordHasher = Substitute.For<ISendPasswordHasher>();
                passwordHasher.PasswordHashMatches(passwordHash, wrongClientPasswordHash)
                    .Returns(false);
                services.AddSingleton(passwordHasher);
            });
        }).CreateClient();

        var requestBody = CreateTokenRequestBody(sendId, wrongClientPasswordHash);

        // Act
        var response = await client.PostAsync("/connect/token", requestBody);

        // Assert
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains(OidcConstants.TokenErrors.InvalidGrant, content);
        Assert.Contains($"{SendAccessConstants.TokenRequest.ClientB64HashedPassword} is invalid", content);
    }

    [Fact]
    public async Task SendAccess_PasswordProtectedSend_MissingPassword_ReturnsInvalidRequest()
    {
        // Arrange
        var sendId = Guid.NewGuid();
        var passwordHash = "stored-password-hash";

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var featureService = Substitute.For<IFeatureService>();
                featureService.IsEnabled(Arg.Any<string>()).Returns(true);
                services.AddSingleton(featureService);

                var sendAuthQuery = Substitute.For<ISendAuthenticationQuery>();
                sendAuthQuery.GetAuthenticationMethod(sendId)
                    .Returns(new ResourcePassword(passwordHash));
                services.AddSingleton(sendAuthQuery);

                var passwordHasher = Substitute.For<ISendPasswordHasher>();
                services.AddSingleton(passwordHasher);
            });
        }).CreateClient();

        var requestBody = CreateTokenRequestBody(sendId); // No password

        // Act
        var response = await client.PostAsync("/connect/token", requestBody);

        // Assert
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains(OidcConstants.TokenErrors.InvalidRequest, content);
        Assert.Contains($"{SendAccessConstants.TokenRequest.ClientB64HashedPassword} is required", content);
    }

    /// <summary>
    /// When the password has is empty or whitespace it doesn't get passed to the server when the request is made.
    /// This leads to an invalid request error since the absence of the password hash is considered a malformed request.
    /// In the case that the passwordB64Hash _is_ empty or whitespace it would be an invalid grant since the request
    /// has the correct shape.
    /// </summary>
    [Fact]
    public async Task SendAccess_PasswordProtectedSend_EmptyPassword_ReturnsInvalidRequest()
    {
        // Arrange
        var sendId = Guid.NewGuid();
        var passwordHash = "stored-password-hash";

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var featureService = Substitute.For<IFeatureService>();
                featureService.IsEnabled(Arg.Any<string>()).Returns(true);
                services.AddSingleton(featureService);

                var sendAuthQuery = Substitute.For<ISendAuthenticationQuery>();
                sendAuthQuery.GetAuthenticationMethod(sendId)
                    .Returns(new ResourcePassword(passwordHash));
                services.AddSingleton(sendAuthQuery);

                // Mock password hasher to return false for empty passwords
                var passwordHasher = Substitute.For<ISendPasswordHasher>();
                passwordHasher.PasswordHashMatches(passwordHash, string.Empty)
                    .Returns(false);
                services.AddSingleton(passwordHasher);
            });
        }).CreateClient();

        var requestBody = CreateTokenRequestBody(sendId, string.Empty);

        // Act
        var response = await client.PostAsync("/connect/token", requestBody);

        // Assert
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains(OidcConstants.TokenErrors.InvalidRequest, content);
        Assert.Contains($"{SendAccessConstants.TokenRequest.ClientB64HashedPassword} is required", content);
    }

    private static FormUrlEncodedContent CreateTokenRequestBody(Guid sendId, string passwordHash = null)
    {
        var sendIdBase64 = CoreHelpers.Base64UrlEncode(sendId.ToByteArray());
        var parameters = new List<KeyValuePair<string, string>>
        {
            new(OidcConstants.TokenRequest.GrantType, CustomGrantTypes.SendAccess),
            new(OidcConstants.TokenRequest.ClientId, BitwardenClient.Send),
            new(SendAccessConstants.TokenRequest.SendId, sendIdBase64),
            new(OidcConstants.TokenRequest.Scope, ApiScopes.ApiSendAccess),
            new("deviceType", "10")
        };

        if (passwordHash != null)
        {
            parameters.Add(new KeyValuePair<string, string>(SendAccessConstants.TokenRequest.ClientB64HashedPassword, passwordHash));
        }

        return new FormUrlEncodedContent(parameters);
    }
}
