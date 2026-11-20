using Bit.Core.Enums;
using Bit.IntegrationTestCommon.Factories;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static Bit.Identity.IntegrationTest.Helpers.PasswordGrantTestHelpers;
using static Bit.Identity.IntegrationTest.Helpers.RefreshTokenGrantTestHelpers;
using static Bit.Identity.IntegrationTest.Helpers.TokenResponseAssertions;

#nullable enable

namespace Bit.Identity.IntegrationTest.TokenEndpoint.Grants.RefreshToken;

/// <summary>
/// Integration tests for a successful <c>refresh_token</c> grant: the response body, refresh token reuse, the access
/// token lifetime, and what a refresh does not record as a login.
/// </summary>
public class RefreshTokenGrantResponseTests
{
    /// <summary>
    /// A valid refresh token gets a new access token in the default token response.
    /// </summary>
    [Fact]
    public async Task RefreshToken_Success()
    {
        using var factory = new IdentityApplicationFactory();
        var (_, _, refreshToken) = await RegisterAndLoginWithPasswordAsync(factory);

        var context = await PostRefreshTokenAsync(factory, refreshToken);

        using var body = await AssertDefaultTokenBodyAsync(context);
        AssertRefreshTokenExists(body.RootElement);
    }

    /// <summary>
    /// A refresh returns only the token fields. The account data that a login returns, such as keys, KDF settings,
    /// and decryption options, is not sent again.
    /// </summary>
    [Fact]
    public async Task RefreshToken_ReturnsOnlyTokenFields()
    {
        using var factory = new IdentityApplicationFactory();
        var (_, _, refreshToken) = await RegisterAndLoginWithPasswordAsync(factory);

        var context = await PostRefreshTokenAsync(factory, refreshToken);

        using var body = await AssertDefaultTokenBodyAsync(context);
        var fields = body.RootElement.EnumerateObject().Select(p => p.Name).Order().ToArray();
        Assert.Equal(new[] { "access_token", "expires_in", "refresh_token", "scope", "token_type" }, fields);
    }

    /// <summary>
    /// Refresh tokens are reusable: a refresh returns the refresh token that the client sent, not a new one.
    /// </summary>
    [Fact]
    public async Task RefreshToken_ReturnsSameRefreshToken()
    {
        using var factory = new IdentityApplicationFactory();
        var (_, _, refreshToken) = await RegisterAndLoginWithPasswordAsync(factory);

        var context = await PostRefreshTokenAsync(factory, refreshToken);

        using var body = await AssertDefaultTokenBodyAsync(context);
        Assert.Equal(refreshToken, AssertRefreshTokenExists(body.RootElement));
    }

    /// <summary>
    /// A refresh token stays valid after a refresh, so the client can use it again.
    /// </summary>
    [Fact]
    public async Task RefreshToken_UsedAgain_Succeeds()
    {
        using var factory = new IdentityApplicationFactory();
        var (_, _, refreshToken) = await RegisterAndLoginWithPasswordAsync(factory);

        var firstContext = await PostRefreshTokenAsync(factory, refreshToken);
        using var firstBody = await AssertDefaultTokenBodyAsync(firstContext);

        var secondContext = await PostRefreshTokenAsync(factory, refreshToken);
        using var secondBody = await AssertDefaultTokenBodyAsync(secondContext);
        Assert.Equal(refreshToken, AssertRefreshTokenExists(secondBody.RootElement));
    }

    /// <summary>
    /// A refreshed access token has the built-in access token lifetime of the client that sent the request.
    /// </summary>
    [Theory]
    [InlineData(BitwardenClient.Web, 3600)] // 1 hour
    [InlineData(BitwardenClient.Browser, 3600)] // 1 hour
    [InlineData(BitwardenClient.Desktop, 3600)] // 1 hour
    [InlineData(BitwardenClient.Mobile, 3600)] // 1 hour
    [InlineData(BitwardenClient.Cli, 3600)] // 1 hour
    [InlineData(BitwardenClient.DirectoryConnector, 24 * 3600)]
    public async Task RefreshToken_AccessTokenLifetime_MatchesClient(string clientId, int expectedExpiresIn)
    {
        using var factory = new IdentityApplicationFactory();
        var (_, _, refreshToken) = await RegisterAndLoginWithPasswordAsync(factory, clientId);

        var context = await PostRefreshTokenAsync(factory, refreshToken, clientId);

        using var body = await AssertDefaultTokenBodyAsync(context, expectedExpiresIn: expectedExpiresIn);
    }

    /// <summary>
    /// The access token lifetime setting lets a deployment, such as a self-hosted or FedRAMP instance, set its own
    /// access token lifetime. The setting applies to every interactive client. The Directory Connector keeps its own
    /// lifetime.
    /// </summary>
    [Theory]
    [InlineData(BitwardenClient.Web, 1200)] // 20 min
    [InlineData(BitwardenClient.Mobile, 1200)] // 20 min
    [InlineData(BitwardenClient.DirectoryConnector, 24 * 3600)]
    public async Task RefreshToken_AccessTokenLifetimeSetting_AppliesToInteractiveClients(
        string clientId, int expectedExpiresIn)
    {
        using var factory = new IdentityApplicationFactory();
        factory.UpdateConfiguration("globalSettings:identityServer:accessTokenLifetimeSeconds", "1200"); // 20 min
        var (_, _, refreshToken) = await RegisterAndLoginWithPasswordAsync(factory, clientId);

        var context = await PostRefreshTokenAsync(factory, refreshToken, clientId);

        using var body = await AssertDefaultTokenBodyAsync(context, expectedExpiresIn: expectedExpiresIn);
    }

    /// <summary>
    /// A refresh renews a session; it is not a login. It logs no login event.
    /// </summary>
    [Fact]
    public async Task RefreshToken_LogsNoLoginEvent()
    {
        using var factory = new IdentityApplicationFactory();
        var (user, _, refreshToken) = await RegisterAndLoginWithPasswordAsync(factory);
        // The login's own event shows that the count finds login events.
        Assert.Equal(1, await CountLoginEventsAsync(factory, user.Id));

        var context = await PostRefreshTokenAsync(factory, refreshToken);

        using var body = await AssertDefaultTokenBodyAsync(context);
        Assert.Equal(1, await CountLoginEventsAsync(factory, user.Id));
    }

    /// <summary>
    /// A refresh renews a session; it is not a login. It does not reset the failed login count, which only a
    /// successful login resets.
    /// </summary>
    [Fact]
    public async Task RefreshToken_KeepsFailedLoginCount()
    {
        using var factory = new IdentityApplicationFactory();
        var (user, _, refreshToken) = await RegisterAndLoginWithPasswordAsync(factory);
        // A successful login resets this count to 0.
        await UpdateUserAsync(factory, user.Id, u => u.FailedLoginCount = 2);

        var context = await PostRefreshTokenAsync(factory, refreshToken);

        using var body = await AssertDefaultTokenBodyAsync(context);
        var userAfter = await factory.GetDatabaseContext().Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);
        Assert.Equal(2, userAfter.FailedLoginCount);
    }

    private static async Task<int> CountLoginEventsAsync(IdentityApplicationFactory factory, Guid userId)
    {
        return await factory.GetDatabaseContext().Events
            .CountAsync(e => e.UserId == userId && e.Type == EventType.User_LoggedIn);
    }
}
