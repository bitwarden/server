using System.Net;
using System.Text.Json;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Auth.Models.Api.Response.Accounts;
using Bit.Core.Auth.Repositories;
using Bit.Core.Enums;
using Bit.Core.Test.Auth.AutoFixture;
using Bit.Core.Utilities;
using Bit.Identity.IntegrationTest.Grants;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Fakes;
using Duende.IdentityModel;
using Xunit;

namespace Bit.Identity.IntegrationTest.RequestValidation.VaultAccess;

public class WebAuthnGrantValidatorTests(IdentityApplicationFactory _factory) : IClassFixture<IdentityApplicationFactory>
{
    private const string _rpId = "localhost";
    private const string _origin = "https://localhost:8080";
    private const DeviceType _deviceType = DeviceType.ChromeBrowser;

    private static readonly KeysRequestModel _testAccountKeys = new()
    {
        AccountKeys = null,
        PublicKey = "public-key",
        EncryptedPrivateKey = "encrypted-private-key",
    };

    [Fact]
    public async Task WebAuthnGrant_MissingToken_ReturnsInvalidGrant()
    {
        // Arrange
        var client = _factory.CreateClient();
        var requestBody = BuildTokenRequest(token: null, deviceResponse: null);

        // Act
        var response = await client.PostAsync("/connect/token", requestBody);

        // Assert
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains(OidcConstants.TokenErrors.InvalidGrant, content);
    }

    [Fact]
    public async Task WebAuthnGrant_InvalidToken_ReturnsInvalidRequest()
    {
        // Arrange
        var client = _factory.CreateClient();
        var requestBody = BuildTokenRequest(token: "not-a-real-token", deviceResponse: "{}");

        // Act
        var response = await client.PostAsync("/connect/token", requestBody);

        // Assert
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains(OidcConstants.TokenErrors.InvalidRequest, content);
    }

    [Fact]
    public async Task AssertionOptions_ReturnsTokenAndOptions()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/accounts/webauthn/assertion-options");

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        var options = await response.Content.ReadFromJsonAsync<WebAuthnLoginAssertionOptionsResponseModel>();
        Assert.NotNull(options);
        Assert.NotEmpty(options.Token);
        Assert.NotNull(options.Options);
        Assert.NotEmpty(options.Options.Challenge);
    }

    [Theory, BitAutoData, RegisterFinishRequestModelCustomize]
    public async Task WebAuthnGrant_Assertion_Succeeds(RegisterFinishRequestModel requestModel)
    {
        // Arrange
        using var context = await SetupWebAuthnLoginAsync(requestModel);

        // Act
        var response = await context.PostTokenAsync();
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.True(response.IsSuccessStatusCode, $"Expected 200, got {response.StatusCode}. Body: {content}");
        Assert.Contains("access_token", content);
    }

    /// <summary>
    /// The login access token names the device the user logged in on, and a refreshed access token keeps it. API
    /// endpoints read the requesting device from these claims.
    /// </summary>
    [Theory, BitAutoData, RegisterFinishRequestModelCustomize]
    public async Task WebAuthnGrant_DeviceClaims_PersistThroughRefresh(RegisterFinishRequestModel requestModel)
    {
        // Arrange
        using var context = await SetupWebAuthnLoginAsync(requestModel);
        var loginResponse = await context.PostTokenAsync();
        Assert.True(loginResponse.IsSuccessStatusCode, await loginResponse.Content.ReadAsStringAsync());
        using var loginBody = await JsonDocument.ParseAsync(await loginResponse.Content.ReadAsStreamAsync());
        var loginAccessToken = loginBody.RootElement.GetProperty("access_token").GetString()!;
        var refreshToken = loginBody.RootElement.GetProperty("refresh_token").GetString()!;
        AccessTokenAssertions.AssertDeviceClaims(
            loginAccessToken, IdentityApplicationFactory.DefaultDeviceIdentifier, _deviceType);

        // Act
        var refreshResponse = await context.PostRefreshTokenAsync(refreshToken);

        // Assert
        Assert.True(refreshResponse.IsSuccessStatusCode, await refreshResponse.Content.ReadAsStringAsync());
        using var refreshBody = await JsonDocument.ParseAsync(await refreshResponse.Content.ReadAsStreamAsync());
        var refreshedAccessToken = refreshBody.RootElement.GetProperty("access_token").GetString()!;
        AccessTokenAssertions.AssertDeviceClaims(
            refreshedAccessToken, IdentityApplicationFactory.DefaultDeviceIdentifier, _deviceType);
    }

    /// <summary>
    /// A WebAuthn login that names no device is rejected, so no access token is ever issued without device claims.
    /// </summary>
    [Theory, BitAutoData, RegisterFinishRequestModelCustomize]
    public async Task WebAuthnGrant_WithoutDevice_IsRejected(RegisterFinishRequestModel requestModel)
    {
        // Arrange
        using var context = await SetupWebAuthnLoginAsync(requestModel);

        // Act
        var response = await context.PostTokenAsync(includeDevice: false);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.False(body.RootElement.TryGetProperty("access_token", out _));
        Assert.Equal("no device information provided",
            body.RootElement.GetProperty("ErrorModel").GetProperty("Message").GetString());
    }

    [Theory, BitAutoData, RegisterFinishRequestModelCustomize]
    public async Task WebAuthnGrant_Assertion_SecondCallRejected(RegisterFinishRequestModel requestModel)
    {
        // Arrange
        using var context = await SetupWebAuthnLoginAsync(requestModel);

        // Act — first call succeeds, second call is rejected by the challenge cache
        var successResponse = await context.PostTokenAsync();
        var rejectedResponse = await context.PostTokenAsync();

        // Assert
        var firstContent = await successResponse.Content.ReadAsStringAsync();
        Assert.True(successResponse.IsSuccessStatusCode, $"First call expected 200, got {successResponse.StatusCode}. Body: {firstContent}");
        Assert.Contains("access_token", firstContent);

        Assert.False(rejectedResponse.IsSuccessStatusCode);
    }

    /// <summary>
    /// Registers a user, seeds a WebAuthn credential, fetches assertion options, and signs a matching device response.
    /// The returned context exposes a single <see cref="WebAuthnLoginContext.PostTokenAsync"/> call.
    /// </summary>
    private static async Task<WebAuthnLoginContext> SetupWebAuthnLoginAsync(RegisterFinishRequestModel requestModel)
    {
        requestModel.UserAsymmetricKeys = _testAccountKeys;
        var factory = new IdentityApplicationFactory();
        var user = await factory.RegisterNewIdentityFactoryUserAsync(requestModel);

        var authenticator = new FakeWebAuthnAuthenticator();
        await SeedCredentialAsync(factory, user.Id, authenticator);

        var client = factory.CreateClient();

        var optionsResponse = await client.GetAsync("/accounts/webauthn/assertion-options");
        Assert.True(optionsResponse.IsSuccessStatusCode);
        var options = await optionsResponse.Content.ReadFromJsonAsync<WebAuthnLoginAssertionOptionsResponseModel>();
        Assert.NotNull(options);

        var deviceResponse = authenticator.MakeAssertion(
            challenge: options.Options.Challenge,
            rpId: _rpId,
            origin: _origin,
            userHandle: user.Id.ToByteArray());
        var deviceResponseJson = JsonSerializer.Serialize(deviceResponse);

        return new WebAuthnLoginContext(factory, client, authenticator, options.Token, deviceResponseJson);
    }

    private static async Task SeedCredentialAsync(
        IdentityApplicationFactory factory,
        Guid userId,
        FakeWebAuthnAuthenticator authenticator)
    {
        using var scope = factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IWebAuthnCredentialRepository>();
        var credential = new WebAuthnCredential
        {
            UserId = userId,
            Name = "test-credential",
            CredentialId = CoreHelpers.Base64UrlEncode(authenticator.CredentialId),
            PublicKey = CoreHelpers.Base64UrlEncode(authenticator.GetCosePublicKey()),
            Counter = 0,
            Type = "public-key",
            AaGuid = Guid.Empty,
            SupportsPrf = false,
        };
        await repo.CreateAsync(credential);
    }

    private static FormUrlEncodedContent BuildTokenRequest(string? token, string? deviceResponse,
        bool includeDevice = true)
    {
        var fields = new Dictionary<string, string>
        {
            { OidcConstants.TokenRequest.GrantType, "webauthn" },
            { OidcConstants.TokenRequest.ClientId, "web" },
            { "scope", "api offline_access" },
        };
        if (includeDevice)
        {
            fields["deviceIdentifier"] = IdentityApplicationFactory.DefaultDeviceIdentifier;
            fields["deviceType"] = ((int)_deviceType).ToString();
            fields["deviceName"] = "chrome";
        }
        if (token != null)
        {
            fields["token"] = token;
        }
        if (deviceResponse != null)
        {
            fields["deviceResponse"] = deviceResponse;
        }
        return new FormUrlEncodedContent(fields);
    }

    private sealed class WebAuthnLoginContext(
        IdentityApplicationFactory factory,
        HttpClient client,
        FakeWebAuthnAuthenticator authenticator,
        string token,
        string deviceResponseJson) : IDisposable
    {
        public Task<HttpResponseMessage> PostTokenAsync(bool includeDevice = true)
            => client.PostAsync("/connect/token", BuildTokenRequest(token, deviceResponseJson, includeDevice));

        public Task<HttpResponseMessage> PostRefreshTokenAsync(string refreshToken)
            => client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { OidcConstants.TokenRequest.GrantType, "refresh_token" },
                { OidcConstants.TokenRequest.ClientId, "web" },
                { "refresh_token", refreshToken },
            }));

        public void Dispose()
        {
            authenticator.Dispose();
            client.Dispose();
            factory.Dispose();
        }
    }
}
