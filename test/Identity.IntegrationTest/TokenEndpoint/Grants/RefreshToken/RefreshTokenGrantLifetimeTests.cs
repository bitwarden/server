using Bit.Core.Enums;
using Bit.IntegrationTestCommon.Factories;
using Xunit;
using static Bit.Identity.IntegrationTest.Helpers.RefreshTokenGrantTestHelpers;
using static Bit.Identity.IntegrationTest.Helpers.TokenResponseAssertions;

#nullable enable

namespace Bit.Identity.IntegrationTest.TokenEndpoint.Grants.RefreshToken;

/// <summary>
/// Integration tests for how long a refresh token stays valid. By default the lifetime slides: each use moves the
/// expiry to a fixed window after the use. A setting changes this to an absolute lifetime that use does not extend.
/// </summary>
public class RefreshTokenGrantLifetimeTests
{
    private static readonly TimeSpan _tolerance = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Each use of a refresh token moves its expiry to the client's sliding window after the use.
    /// </summary>
    [Theory]
    [InlineData(BitwardenClient.Web, 7)]
    [InlineData(BitwardenClient.Browser, 30)]
    [InlineData(BitwardenClient.Desktop, 30)]
    [InlineData(BitwardenClient.Mobile, 60)]
    [InlineData(BitwardenClient.Cli, 30)]
    [InlineData(BitwardenClient.DirectoryConnector, 30)]
    public async Task RefreshToken_Use_MovesExpiryToClientWindowAfterUse(string clientId, int windowDays)
    {
        using var factory = new IdentityApplicationFactory();
        var (user, _, refreshToken) = await RegisterAndLoginAsync(factory, clientId);
        // Login already set the expiry to now + window. Move it, so the refresh must set it again.
        await SetRefreshTokenGrantExpirationAsync(factory, user.Id, DateTime.UtcNow.AddHours(1));

        var context = await PostRefreshTokenAsync(factory, refreshToken, clientId);

        using var body = await AssertDefaultTokenBodyAsync(context, expectedExpiresIn: ExpectedExpiresIn(clientId));
        var grant = await GetRefreshTokenGrantAsync(factory, user.Id);
        Assert.NotNull(grant.ExpirationDate);
        Assert.Equal(DateTime.UtcNow.AddDays(windowDays), grant.ExpirationDate.Value, _tolerance);
    }

    /// <summary>
    /// The sliding lifetime setting lets a deployment, such as a self-hosted or FedRAMP instance, set its own sliding
    /// window. The setting replaces the built-in window of each client.
    /// </summary>
    [Fact]
    public async Task RefreshToken_SlidingLifetimeSetting_SetsTheWindow()
    {
        using var factory = new IdentityApplicationFactory();
        factory.UpdateConfiguration(
            "globalSettings:identityServer:slidingRefreshTokenLifetimeSeconds", "7200"); // 2 hours
        var (user, _, refreshToken) = await RegisterAndLoginAsync(factory);
        // Login already set the expiry to now + 2 h. Move it, so the refresh must set it again.
        await SetRefreshTokenGrantExpirationAsync(factory, user.Id, DateTime.UtcNow.AddMinutes(30));

        var context = await PostRefreshTokenAsync(factory, refreshToken);

        using var body = await AssertDefaultTokenBodyAsync(context);
        var grant = await GetRefreshTokenGrantAsync(factory, user.Id);
        Assert.NotNull(grant.ExpirationDate);
        Assert.Equal(DateTime.UtcNow.AddHours(2), grant.ExpirationDate.Value, _tolerance);
    }

    /// <summary>
    /// The absolute lifetime setting lets a deployment, such as a self-hosted or FedRAMP instance, end each session at
    /// a fixed time after the login, however often the client refreshes. The sliding window here is shorter than the
    /// absolute lifetime, so the expiry shows which mode applies.
    /// </summary>
    [Fact]
    public async Task RefreshToken_AbsoluteLifetimeSetting_UseDoesNotMoveExpiry()
    {
        using var factory = new IdentityApplicationFactory();
        factory.UpdateConfiguration("globalSettings:identityServer:applyAbsoluteExpirationOnRefreshToken", "true");
        factory.UpdateConfiguration(
            "globalSettings:identityServer:absoluteRefreshTokenLifetimeSeconds", "259200"); // 3 days
        factory.UpdateConfiguration(
            "globalSettings:identityServer:slidingRefreshTokenLifetimeSeconds", "3600"); // 1 hour
        var (user, _, refreshToken) = await RegisterAndLoginAsync(factory);
        var grantBefore = await GetRefreshTokenGrantAsync(factory, user.Id);
        Assert.NotNull(grantBefore.ExpirationDate);
        // Login sets the expiry from the 3-day absolute lifetime, not the 1 h sliding window.
        Assert.Equal(grantBefore.CreationDate.AddDays(3), grantBefore.ExpirationDate.Value, _tolerance);

        var context = await PostRefreshTokenAsync(factory, refreshToken);

        using var body = await AssertDefaultTokenBodyAsync(context);
        var grantAfter = await GetRefreshTokenGrantAsync(factory, user.Id);
        Assert.Equal(grantBefore.ExpirationDate, grantAfter.ExpirationDate);
    }

    /// <summary>
    /// With the absolute lifetime setting, a refresh token is rejected after the absolute lifetime ends, even when the
    /// client refreshed in time.
    /// </summary>
    [Fact]
    public async Task RefreshToken_AfterAbsoluteLifetime_IsRejected()
    {
        using var factory = new IdentityApplicationFactory();
        factory.UpdateConfiguration("globalSettings:identityServer:applyAbsoluteExpirationOnRefreshToken", "true");
        factory.UpdateConfiguration("globalSettings:identityServer:absoluteRefreshTokenLifetimeSeconds", "3");
        factory.UpdateConfiguration(
            "globalSettings:identityServer:slidingRefreshTokenLifetimeSeconds", "3600"); // 1 hour
        var (_, _, refreshToken) = await RegisterAndLoginAsync(factory);
        var firstContext = await PostRefreshTokenAsync(factory, refreshToken);
        using var firstBody = await AssertDefaultTokenBodyAsync(firstContext);
        // The server checks the lifetime in the stored grant data, so the test waits it out.
        await Task.Delay(TimeSpan.FromSeconds(4));

        var context = await PostRefreshTokenAsync(factory, refreshToken);

        // The 1 h sliding window has not ended, so only the absolute lifetime can reject the token.
        await AssertInvalidGrantAsync(context);
    }

    /// <summary>
    /// In sliding mode, the absolute lifetime setting caps the sliding window, so an active session still ends at a
    /// fixed time after the login.
    /// </summary>
    [Fact]
    public async Task RefreshToken_AbsoluteLifetimeInSlidingMode_CapsTheWindow()
    {
        using var factory = new IdentityApplicationFactory();
        factory.UpdateConfiguration("globalSettings:identityServer:applyAbsoluteExpirationOnRefreshToken", "false");
        factory.UpdateConfiguration(
            "globalSettings:identityServer:absoluteRefreshTokenLifetimeSeconds", "3600"); // 1 hour
        factory.UpdateConfiguration(
            "globalSettings:identityServer:slidingRefreshTokenLifetimeSeconds", "7200"); // 2 hours
        var (user, _, refreshToken) = await RegisterAndLoginAsync(factory);

        var context = await PostRefreshTokenAsync(factory, refreshToken);

        using var body = await AssertDefaultTokenBodyAsync(context);
        var grant = await GetRefreshTokenGrantAsync(factory, user.Id);
        Assert.NotNull(grant.ExpirationDate);
        // The 2 h window would end after the 1 h cap, so the cap sets the expiry.
        Assert.Equal(grant.CreationDate.AddHours(1), grant.ExpirationDate.Value, _tolerance);
    }

    /// <summary>
    /// In sliding mode, a refresh token is rejected after the absolute lifetime cap ends, even when the client
    /// refreshed in time.
    /// </summary>
    [Fact]
    public async Task RefreshToken_AfterAbsoluteCapInSlidingMode_IsRejected()
    {
        using var factory = new IdentityApplicationFactory();
        factory.UpdateConfiguration("globalSettings:identityServer:applyAbsoluteExpirationOnRefreshToken", "false");
        factory.UpdateConfiguration("globalSettings:identityServer:absoluteRefreshTokenLifetimeSeconds", "3");
        factory.UpdateConfiguration(
            "globalSettings:identityServer:slidingRefreshTokenLifetimeSeconds", "3600"); // 1 hour
        var (_, _, refreshToken) = await RegisterAndLoginAsync(factory);
        var firstContext = await PostRefreshTokenAsync(factory, refreshToken);
        using var firstBody = await AssertDefaultTokenBodyAsync(firstContext);
        // The server checks the lifetime in the stored grant data, so the test waits it out.
        await Task.Delay(TimeSpan.FromSeconds(4));

        var context = await PostRefreshTokenAsync(factory, refreshToken);

        // The refresh moved the 1 h sliding window, so only the cap can reject the token.
        await AssertInvalidGrantAsync(context);
    }

    /// <summary>
    /// A refresh token that is past its expiry is rejected.
    /// </summary>
    [Fact]
    public async Task RefreshToken_Expired_IsRejected()
    {
        using var factory = new IdentityApplicationFactory();
        factory.UpdateConfiguration("globalSettings:identityServer:slidingRefreshTokenLifetimeSeconds", "1");
        var (_, _, refreshToken) = await RegisterAndLoginAsync(factory);
        // The server checks the lifetime in the stored grant data, so the test waits it out.
        await Task.Delay(TimeSpan.FromSeconds(2));

        var context = await PostRefreshTokenAsync(factory, refreshToken);

        await AssertInvalidGrantAsync(context);
    }

    private static int ExpectedExpiresIn(string clientId) =>
        clientId == BitwardenClient.DirectoryConnector ? 24 * 3600 : 3600; // 24 hours : 1 hour
}
