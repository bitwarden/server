using Bit.Core.Auth.Identity;
using Bit.IntegrationTestCommon.Factories;
using Duende.IdentityModel;
using Xunit;
using static Bit.Identity.IntegrationTest.Helpers.AccessTokenAssertions;
using static Bit.Identity.IntegrationTest.Helpers.PasswordGrantTestHelpers;
using static Bit.Identity.IntegrationTest.Helpers.RefreshTokenGrantTestHelpers;
using static Bit.Identity.IntegrationTest.Helpers.TokenResponseAssertions;

#nullable enable

namespace Bit.Identity.IntegrationTest.TokenEndpoint.Grants.RefreshToken;

/// <summary>
/// Integration tests for the user claims on a refreshed access token. Account claims are read again from the
/// database. Login and device claims keep their values from the login.
/// </summary>
public class RefreshTokenGrantUserClaimsTests
{
    /// <summary>
    /// A refresh reads the account claims again from the database, so changes made after the login appear in the new
    /// access token.
    /// </summary>
    [Fact]
    public async Task RefreshToken_RebuildsAccountClaimsFromDatabase()
    {
        using var factory = new IdentityApplicationFactory();
        var (user, accessToken, refreshToken) = await RegisterAndLoginWithPasswordAsync(factory);
        var loginClaims = ReadPayload(accessToken);
        // Each new value differs from the login value, so the values after the refresh can only come from the database.
        Assert.Equal("false", loginClaims.GetProperty(Claims.Premium).ToString().ToLowerInvariant());
        Assert.Equal("true", loginClaims.GetProperty(JwtClaimTypes.EmailVerified).ToString().ToLowerInvariant());
        await UpdateUserAsync(factory, user.Id, u =>
        {
            u.Premium = true;
            u.EmailVerified = false;
            u.Name = "Renamed User";
            u.Email = "renamed@email.qa";
        });

        var context = await PostRefreshTokenAsync(factory, refreshToken);

        using var body = await AssertDefaultTokenBodyAsync(context);
        var refreshedClaims = ReadPayload(AssertAccessTokenExists(body.RootElement));
        Assert.Equal("true", refreshedClaims.GetProperty(Claims.Premium).ToString().ToLowerInvariant());
        Assert.Equal("false", refreshedClaims.GetProperty(JwtClaimTypes.EmailVerified).ToString().ToLowerInvariant());
        Assert.Equal("Renamed User", refreshedClaims.GetProperty(JwtClaimTypes.Name).GetString());
        Assert.Equal("renamed@email.qa", refreshedClaims.GetProperty(JwtClaimTypes.Email).GetString());
    }

    /// <summary>
    /// A refresh keeps each of these claims with the same value that it had at login.
    /// </summary>
    [Theory]
    [InlineData(JwtClaimTypes.Subject)]
    [InlineData(JwtClaimTypes.AuthenticationMethod)]
    [InlineData(JwtClaimTypes.IdentityProvider)]
    [InlineData(JwtClaimTypes.AuthenticationTime)]
    [InlineData(Claims.SecurityStamp)]
    [InlineData(Claims.Device)]
    [InlineData(Claims.DeviceType)]
    public async Task RefreshToken_KeepsClaimValueFromLogin(string claimType)
    {
        using var factory = new IdentityApplicationFactory();
        var (_, accessToken, refreshToken) = await RegisterAndLoginWithPasswordAsync(factory);
        Assert.True(ReadPayload(accessToken).TryGetProperty(claimType, out var loginValue));

        var context = await PostRefreshTokenAsync(factory, refreshToken);

        using var body = await AssertDefaultTokenBodyAsync(context);
        var refreshedClaims = ReadPayload(AssertAccessTokenExists(body.RootElement));
        Assert.True(refreshedClaims.TryGetProperty(claimType, out var refreshedValue));
        Assert.Equal(loginValue.GetRawText(), refreshedValue.GetRawText());
    }
}
