using System.Globalization;
using System.Text.Json;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Enums;
using Bit.Identity.IdentityServer;
using Bit.Identity.IntegrationTest.Helpers;
using Bit.IntegrationTestCommon;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.Helpers;
using Duende.IdentityServer;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static Bit.Identity.IntegrationTest.Helpers.SsoLoginTestHelpers;

#nullable enable

namespace Bit.Identity.IntegrationTest.TokenEndpoint.Grants.AuthorizationCode;

public class AuthorizationCodeGrantSsoTests
{
    const string SsoDeviceIdentifier = "test_id";

    /// <summary>
    /// API endpoints read the requesting device from the access token's device claims, and today, clients refresh the
    /// access token shortly after an SSO login. The device claims must survive that refresh.
    /// </summary>
    [Fact]
    public async Task SsoLogin_RefreshToken_PreservesDeviceClaims()
    {
        // Arrange
        var (factory, loginRoot) = await SsoLoginAsync();
        var loginAccessToken = AssertHelper.AssertJsonProperty(loginRoot, "access_token", JsonValueKind.String).GetString()!;
        var refreshToken = AssertHelper.AssertJsonProperty(loginRoot, "refresh_token", JsonValueKind.String).GetString()!;
        AccessTokenAssertions.AssertDeviceClaims(loginAccessToken, SsoDeviceIdentifier, DeviceType.FirefoxBrowser);

        // Act
        var refreshContext = await PostRefreshTokenAsync(factory, refreshToken);

        // Assert
        using var refreshBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(refreshContext);
        var refreshedAccessToken = AssertHelper.AssertJsonProperty(
            refreshBody.RootElement, "access_token", JsonValueKind.String).GetString()!;
        AccessTokenAssertions.AssertDeviceClaims(refreshedAccessToken, SsoDeviceIdentifier, DeviceType.FirefoxBrowser);
    }

    /// <summary>
    /// An SSO login that names no device is rejected, so no access token is ever issued without device claims.
    /// </summary>
    [Fact]
    public async Task SsoLogin_WithoutDevice_IsRejected()
    {
        // Act
        var (_, loginContext) = await PostSsoLoginAsync(includeDevice: false);

        // Assert
        Assert.Equal(StatusCodes.Status400BadRequest, loginContext.Response.StatusCode);
        using var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(loginContext);
        Assert.False(body.RootElement.TryGetProperty("access_token", out _));
        var errorModel = AssertHelper.AssertJsonProperty(body.RootElement, "ErrorModel", JsonValueKind.Object);
        Assert.Equal("no device information provided",
            AssertHelper.AssertJsonProperty(errorModel, "Message", JsonValueKind.String).GetString());
    }

    /// <summary>
    /// A user stamp change (e.g. a password change) must end SSO sessions on their next refresh, the same as
    /// password-login sessions.
    /// </summary>
    [Fact]
    public async Task SsoLogin_RefreshToken_AfterUserStampChange_IsRejected()
    {
        // Arrange
        var (factory, loginRoot) = await SsoLoginAsync();
        var refreshToken = AssertHelper.AssertJsonProperty(loginRoot, "refresh_token", JsonValueKind.String).GetString()!;
        await UpdateUserAsync(factory, user => user.SecurityStamp = Guid.NewGuid().ToString());

        // Act
        var refreshContext = await PostRefreshTokenAsync(factory, refreshToken);

        // Assert
        Assert.Equal(StatusCodes.Status400BadRequest, refreshContext.Response.StatusCode);
        using var refreshBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(refreshContext);
        var error = AssertHelper.AssertJsonProperty(refreshBody.RootElement, "error", JsonValueKind.String).GetString();
        Assert.Equal("invalid_grant", error);
    }

    /// <summary>
    /// The authorization code is persisted as a grant and read back by the token endpoint, as it is after a real
    /// SSO login, and the exchange issues both an access token and a refresh token.
    /// </summary>
    [Fact]
    public async Task SsoLogin_AuthorizationCodeFromGrantStore_ReturnsTokens()
    {
        // Arrange
        var challenge = new string('c', 50);
        var (factory, code) = await CreateFactoryAsync(new SsoConfigurationData
        {
            MemberDecryptionType = MemberDecryptionType.MasterPassword,
        }, challenge);

        var database = factory.GetDatabaseContext();
        Assert.Single(await database.Grants
            .Where(g => g.Type == IdentityServerConstants.PersistedGrantTypes.AuthorizationCode)
            .ToListAsync());

        // Act
        var context = await PostAuthorizationCodeAsync(factory, code, challenge, includeDevice: true);

        // Assert
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        using var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        AssertHelper.AssertJsonProperty(body.RootElement, "access_token", JsonValueKind.String);
        AssertHelper.AssertJsonProperty(body.RootElement, "refresh_token", JsonValueKind.String);
    }

    /// <summary>
    /// <see cref="AuthorizationCodeStore.RemoveAuthorizationCodeAsync"/> intentionally keeps a redeemed code until
    /// it expires, so a client can redeem the same code again within its lifetime, for example after a two-factor
    /// challenge.
    /// </summary>
    [Fact]
    public async Task SsoLogin_SameAuthorizationCodeRedeemedTwice_BothSucceed()
    {
        // Arrange
        var challenge = new string('c', 50);
        var (factory, code) = await CreateFactoryAsync(new SsoConfigurationData
        {
            MemberDecryptionType = MemberDecryptionType.MasterPassword,
        }, challenge);

        // Act
        var firstContext = await PostAuthorizationCodeAsync(factory, code, challenge, includeDevice: true);
        var secondContext = await PostAuthorizationCodeAsync(factory, code, challenge, includeDevice: true);

        // Assert
        Assert.Equal(StatusCodes.Status200OK, firstContext.Response.StatusCode);
        Assert.Equal(StatusCodes.Status200OK, secondContext.Response.StatusCode);
        using var secondBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(secondContext);
        AssertHelper.AssertJsonProperty(secondBody.RootElement, "access_token", JsonValueKind.String);
    }

    private static async Task<(IdentityApplicationFactory Factory, JsonElement LoginRoot)> SsoLoginAsync()
    {
        var (factory, loginContext) = await PostSsoLoginAsync(includeDevice: true);
        Assert.Equal(StatusCodes.Status200OK, loginContext.Response.StatusCode);
        using var loginBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(loginContext);
        return (factory, loginBody.RootElement.Clone());
    }

    private static async Task<(IdentityApplicationFactory Factory, HttpContext LoginContext)> PostSsoLoginAsync(
        bool includeDevice)
    {
        var challenge = new string('c', 50);
        var (factory, code) = await CreateFactoryAsync(new SsoConfigurationData
        {
            MemberDecryptionType = MemberDecryptionType.MasterPassword,
        }, challenge);

        var loginContext = await PostAuthorizationCodeAsync(factory, code, challenge, includeDevice);
        return (factory, loginContext);
    }

    private static async Task<HttpContext> PostAuthorizationCodeAsync(IdentityApplicationFactory factory,
        string code, string challenge, bool includeDevice)
    {
        var form = new Dictionary<string, string>
        {
            { "scope", "api offline_access" },
            { "client_id", SsoAuthorizationCodeTestHelpers.ClientId },
            { "twoFactorToken", "TEST" },
            { "twoFactorProvider", "5" }, // RememberMe Provider
            { "twoFactorRemember", "0" },
            { "grant_type", "authorization_code" },
            { "code", code },
            { "code_verifier", challenge },
            { "redirect_uri", SsoAuthorizationCodeTestHelpers.RedirectUri }
        };
        if (includeDevice)
        {
            form["deviceType"] = ((int)DeviceType.FirefoxBrowser).ToString(CultureInfo.InvariantCulture);
            form["deviceIdentifier"] = SsoDeviceIdentifier;
            form["deviceName"] = "firefox";
        }

        using var content = new FormUrlEncodedContent(form);
        return await factory.Server.PostAsync("/connect/token", content);
    }

    private static async Task<HttpContext> PostRefreshTokenAsync(IdentityApplicationFactory factory, string refreshToken)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "grant_type", "refresh_token" },
            { "client_id", "web" },
            { "refresh_token", refreshToken },
        });
        return await factory.Server.PostAsync("/connect/token", content);
    }
}
