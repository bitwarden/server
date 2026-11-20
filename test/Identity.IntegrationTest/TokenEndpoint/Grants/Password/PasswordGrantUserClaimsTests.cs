using System.Text.Json;
using Bit.Core.Auth.Identity;
using Bit.IntegrationTestCommon.Factories;
using Duende.IdentityModel;
using Xunit;
using static Bit.Identity.IntegrationTest.Helpers.AccessTokenAssertions;
using static Bit.Identity.IntegrationTest.Helpers.PasswordGrantTestHelpers;
using static Bit.Identity.IntegrationTest.Helpers.TokenResponseAssertions;
using static Bit.Identity.IntegrationTest.Helpers.UserTestHelpers;

#nullable enable

namespace Bit.Identity.IntegrationTest.TokenEndpoint.Grants.Password;

/// <summary>
/// Integration tests for the user claims on the access token that a password login issues.
/// </summary>
public class PasswordGrantUserClaimsTests
{
    /// <summary>
    /// The access token carries the account claims of the user as stored in the database.
    /// </summary>
    [Fact]
    public async Task PasswordGrant_AccessTokenHasAccountClaims()
    {
        using var factory = new IdentityApplicationFactory();
        var user = await RegisterUserAsync(factory);
        // Registration does not set these values, so the claims can only come from the stored user.
        await UpdateUserAsync(factory, user.Id, u =>
        {
            u.Premium = true;
            u.Name = "Login User";
        });

        var context = await factory.ContextFromPasswordAsync(DefaultUsername, DefaultPassword);

        using var body = await AssertDefaultTokenBodyAsync(context);
        var claims = ReadPayload(AssertAccessTokenExists(body.RootElement));
        Assert.Equal(user.Id.ToString(), claims.GetProperty(JwtClaimTypes.Subject).GetString());
        Assert.Equal(DefaultUsername, claims.GetProperty(JwtClaimTypes.Email).GetString());
        Assert.Equal("true", claims.GetProperty(JwtClaimTypes.EmailVerified).ToString().ToLowerInvariant());
        Assert.Equal("Login User", claims.GetProperty(JwtClaimTypes.Name).GetString());
        Assert.Equal("true", claims.GetProperty(Claims.Premium).ToString().ToLowerInvariant());
        Assert.Equal(user.SecurityStamp, claims.GetProperty(Claims.SecurityStamp).GetString());
    }

    /// <summary>
    /// The access token names the login method and Bitwarden as the identity provider.
    /// </summary>
    [Fact]
    public async Task PasswordGrant_AccessTokenHasLoginMethodClaims()
    {
        using var factory = new IdentityApplicationFactory();
        await RegisterUserAsync(factory);

        var context = await factory.ContextFromPasswordAsync(DefaultUsername, DefaultPassword);

        using var body = await AssertDefaultTokenBodyAsync(context);
        var claims = ReadPayload(AssertAccessTokenExists(body.RootElement));
        var authenticationMethods = claims.GetProperty(JwtClaimTypes.AuthenticationMethod);
        Assert.Equal(JsonValueKind.Array, authenticationMethods.ValueKind);
        Assert.Equal(new[] { "Application" }, authenticationMethods.EnumerateArray().Select(e => e.GetString()));
        Assert.Equal("bitwarden", claims.GetProperty(JwtClaimTypes.IdentityProvider).GetString());
    }
}
