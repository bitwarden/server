using System.Text.Json;
using Bit.Core.Enums;
using Bit.Identity.IdentityServer;
using Bit.Identity.IntegrationTest.Helpers;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.Helpers;
using Duende.IdentityServer;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static Bit.Identity.IntegrationTest.Helpers.AuthorizationCodeGrantTestHelpers;
using static Bit.Identity.IntegrationTest.Helpers.RefreshTokenGrantTestHelpers;
using static Bit.Identity.IntegrationTest.Helpers.TokenResponseAssertions;
using static Bit.Identity.IntegrationTest.Helpers.UserTestHelpers;

#nullable enable

namespace Bit.Identity.IntegrationTest.TokenEndpoint.Grants.AuthorizationCode;

public class AuthorizationCodeGrantSsoTests
{
    /// <summary>
    /// API endpoints read the requesting device from the access token's device claims, and today, clients refresh the
    /// access token shortly after an SSO login. The device claims must survive that refresh.
    /// </summary>
    [Fact]
    public async Task SsoLogin_RefreshToken_PreservesDeviceClaims()
    {
        // Arrange
        using var factory = new IdentityApplicationFactory();
        var (_, code) = await RegisterSsoUserWithAuthorizationCodeAsync(factory);
        var loginContext = await PostAuthorizationCodeAsync(factory, code);
        using var loginBody = await AssertDefaultTokenBodyAsync(loginContext);
        AccessTokenAssertions.AssertDeviceClaims(AssertAccessTokenExists(loginBody.RootElement),
            IdentityApplicationFactory.DefaultDeviceIdentifier, DeviceType.FirefoxBrowser);

        // Act
        var refreshContext = await PostRefreshTokenAsync(factory, AssertRefreshTokenExists(loginBody.RootElement));

        // Assert
        using var refreshBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(refreshContext);
        AccessTokenAssertions.AssertDeviceClaims(AssertAccessTokenExists(refreshBody.RootElement),
            IdentityApplicationFactory.DefaultDeviceIdentifier, DeviceType.FirefoxBrowser);
    }

    /// <summary>
    /// An SSO login that names no device is rejected, so no access token is ever issued without device claims.
    /// </summary>
    [Fact]
    public async Task SsoLogin_WithoutDevice_IsRejected()
    {
        // Arrange
        using var factory = new IdentityApplicationFactory();
        var (_, code) = await RegisterSsoUserWithAuthorizationCodeAsync(factory);

        // Act
        var loginContext = await PostAuthorizationCodeAsync(factory, code, includeDevice: false);

        // Assert
        await AssertErrorModelAsync(loginContext, "no device information provided", expectedError: "device_error");
    }

    /// <summary>
    /// A user stamp change (e.g. a password change) must end SSO sessions on their next refresh, the same as
    /// password-login sessions.
    /// </summary>
    [Fact]
    public async Task SsoLogin_RefreshToken_AfterUserStampChange_IsRejected()
    {
        // Arrange
        using var factory = new IdentityApplicationFactory();
        var (user, code) = await RegisterSsoUserWithAuthorizationCodeAsync(factory);
        var loginContext = await PostAuthorizationCodeAsync(factory, code);
        using var loginBody = await AssertDefaultTokenBodyAsync(loginContext);
        await UpdateUserAsync(factory, user.Id, u => u.SecurityStamp = Guid.NewGuid().ToString());

        // Act
        var refreshContext = await PostRefreshTokenAsync(factory, AssertRefreshTokenExists(loginBody.RootElement));

        // Assert
        await AssertInvalidGrantAsync(refreshContext);
    }

    /// <summary>
    /// The authorization code is persisted as a grant and read back by the token endpoint, as it is after a real
    /// SSO login, and the exchange issues both an access token and a refresh token.
    /// </summary>
    [Fact]
    public async Task SsoLogin_AuthorizationCodeFromGrantStore_ReturnsTokens()
    {
        // Arrange
        using var factory = new IdentityApplicationFactory();
        var (_, code) = await RegisterSsoUserWithAuthorizationCodeAsync(factory);

        var database = factory.GetDatabaseContext();
        Assert.Single(await database.Grants
            .Where(g => g.Type == IdentityServerConstants.PersistedGrantTypes.AuthorizationCode)
            .ToListAsync());

        // Act
        var context = await PostAuthorizationCodeAsync(factory, code);

        // Assert
        using var body = await AssertDefaultTokenBodyAsync(context);
        AssertRefreshTokenExists(body.RootElement);
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
        using var factory = new IdentityApplicationFactory();
        var (_, code) = await RegisterSsoUserWithAuthorizationCodeAsync(factory);

        // Act
        var firstContext = await PostAuthorizationCodeAsync(factory, code);
        var secondContext = await PostAuthorizationCodeAsync(factory, code);

        // Assert
        using var firstBody = await AssertDefaultTokenBodyAsync(firstContext);
        using var secondBody = await AssertDefaultTokenBodyAsync(secondContext);
    }
}
