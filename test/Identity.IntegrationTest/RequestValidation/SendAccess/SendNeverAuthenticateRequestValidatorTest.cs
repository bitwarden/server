using Bit.Core.Services;
using Bit.Core.Tools.Models.Data;
using Bit.Core.Tools.SendFeatures.Queries.Interfaces;
using Bit.Core.Utilities;
using Bit.Identity.IdentityServer.RequestValidators.SendAccess;
using Bit.IntegrationTestCommon.Factories;
using Duende.IdentityModel;
using NSubstitute;
using Xunit;

namespace Bit.Identity.IntegrationTest.RequestValidation.SendAccess;

public class SendNeverAuthenticateRequestValidatorIntegrationTests(
    IdentityApplicationFactory _factory) : IClassFixture<IdentityApplicationFactory>
{
    /// <summary>
    /// To support the static hashing function <see cref="EnumerationProtectionHelpers.GetIndexForSaltHash"/> theses GUIDs and Key must be hardcoded
    /// </summary>
    private static readonly string _testHashKey = "test-key-123456789012345678901234567890";
    // These Guids are static and ensure the correct index for each error type
    private static readonly Guid _invalidSendGuid = Guid.Parse("1b35fbf3-a14a-4d48-82b7-2adc34fdae6f");
    private static readonly Guid _emailSendGuid = Guid.Parse("bc8e2ef5-a0bd-44d2-bdb7-5902be6f5c41");
    private static readonly Guid _passwordSendGuid = Guid.Parse("da36fa27-f0e8-4701-a585-d3d8c2f67c4b");

    [Fact]
    public async Task SendAccess_NeverAuthenticateSend_NoParameters_ReturnsInvalidSendId()
    {
        // Arrange
        var client = ConfigureTestHttpClient(_invalidSendGuid);
        var requestBody = SendAccessTestUtilities.CreateTokenRequestBody(_invalidSendGuid);

        // Act
        var response = await client.PostAsync("/connect/token", requestBody);

        // Assert
        var content = await response.Content.ReadAsStringAsync();

        Assert.Contains(OidcConstants.TokenErrors.InvalidGrant, content);

        var expectedError = SendAccessConstants.GrantValidatorResults.InvalidSendId;
        Assert.Contains(expectedError, content);
    }

    [Fact]
    public async Task SendAccess_NeverAuthenticateSend_ReturnsEmailRequired()
    {
        // Arrange
        var client = ConfigureTestHttpClient(_emailSendGuid);
        var requestBody = SendAccessTestUtilities.CreateTokenRequestBody(_emailSendGuid);

        // Act
        var response = await client.PostAsync("/connect/token", requestBody);

        // Assert
        var content = await response.Content.ReadAsStringAsync();

        // should be invalid grant
        Assert.Contains(OidcConstants.TokenErrors.InvalidRequest, content);

        // Try to compel the invalid email error
        var expectedError = SendAccessConstants.EmailOtpValidatorResults.EmailRequired;
        Assert.Contains(expectedError, content);
    }

    [Fact]
    public async Task SendAccess_NeverAuthenticateSend_WithEmail_ReturnsEmailInvalid()
    {
        // Arrange
        var email = "test@example.com";
        var client = ConfigureTestHttpClient(_emailSendGuid);
        var requestBody = SendAccessTestUtilities.CreateTokenRequestBody(_emailSendGuid, email: email);

        // Act
        var response = await client.PostAsync("/connect/token", requestBody);

        // Assert
        var content = await response.Content.ReadAsStringAsync();

        // should be invalid grant
        Assert.Contains(OidcConstants.TokenErrors.InvalidGrant, content);

        // Try to compel the invalid email error
        var expectedError = SendAccessConstants.EmailOtpValidatorResults.EmailInvalid;
        Assert.Contains(expectedError, content);
    }

    [Fact]
    public async Task SendAccess_NeverAuthenticateSend_ReturnsPasswordRequired()
    {
        // Arrange
        var client = ConfigureTestHttpClient(_passwordSendGuid);

        var requestBody = SendAccessTestUtilities.CreateTokenRequestBody(_passwordSendGuid);

        // Act
        var response = await client.PostAsync("/connect/token", requestBody);

        // Assert
        var content = await response.Content.ReadAsStringAsync();

        Assert.Contains(OidcConstants.TokenErrors.InvalidGrant, content);

        var expectedError = SendAccessConstants.PasswordValidatorResults.RequestPasswordIsRequired;
        Assert.Contains(expectedError, content);
    }

    [Fact]
    public async Task SendAccess_NeverAuthenticateSend_WithPassword_ReturnsPasswordInvalid()
    {
        // Arrange
        var password = "test-password-hash";

        var client = ConfigureTestHttpClient(_passwordSendGuid);

        var requestBody = SendAccessTestUtilities.CreateTokenRequestBody(_passwordSendGuid, password: password);

        // Act
        var response = await client.PostAsync("/connect/token", requestBody);

        // Assert
        var content = await response.Content.ReadAsStringAsync();

        Assert.Contains(OidcConstants.TokenErrors.InvalidRequest, content);

        var expectedError = SendAccessConstants.PasswordValidatorResults.RequestPasswordDoesNotMatch;
        Assert.Contains(expectedError, content);
    }

    [Fact]
    public async Task SendAccess_NeverAuthenticateSend_ConsistentResponse_SameSendId()
    {
        // Arrange
        var client = ConfigureTestHttpClient(_emailSendGuid);

        var requestBody1 = SendAccessTestUtilities.CreateTokenRequestBody(_emailSendGuid);
        var requestBody2 = SendAccessTestUtilities.CreateTokenRequestBody(_emailSendGuid);

        // Act
        var response1 = await client.PostAsync("/connect/token", requestBody1);
        var response2 = await client.PostAsync("/connect/token", requestBody2);

        // Assert
        var content1 = await response1.Content.ReadAsStringAsync();
        var content2 = await response2.Content.ReadAsStringAsync();

        Assert.Equal(content1, content2);
    }

    private HttpClient ConfigureTestHttpClient(Guid sendId)
    {
        _factory.UpdateConfiguration(
            "globalSettings:sendDefaultHashKey", _testHashKey);
        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var featureService = Substitute.For<IFeatureService>();
                featureService.IsEnabled(Arg.Any<string>()).Returns(true);
                services.AddSingleton(featureService);

                var sendAuthQuery = Substitute.For<ISendAuthenticationQuery>();
                sendAuthQuery.GetAuthenticationMethod(sendId)
                    .Returns(new NeverAuthenticate());
                services.AddSingleton(sendAuthQuery);
            });
        }).CreateClient();
    }
}
