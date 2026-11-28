using System.Text.Json;
using Bit.Core.Auth.Identity;
using Bit.Core.Repositories;
using Bit.IntegrationTestCommon.Factories;
using Duende.IdentityModel;
using Xunit;
using static Bit.Identity.IntegrationTest.Helpers.AccessTokenAssertions;
using static Bit.Identity.IntegrationTest.Helpers.AuthorizationCodeGrantTestHelpers;
using static Bit.Identity.IntegrationTest.Helpers.TokenResponseAssertions;

#nullable enable

namespace Bit.Identity.IntegrationTest.TokenEndpoint.Grants.AuthorizationCode;

/// <summary>
/// Integration tests for the user claims on the access token that an SSO login issues. The tests store the
/// authorization code directly instead of running the SSO login, so they cover only the token request.
/// </summary>
public class AuthorizationCodeGrantUserClaimsTests
{
    /// <summary>
    /// The access token carries the account claims and the organization membership of the user as stored in the
    /// database, and the login method and identity provider of the SSO login.
    /// </summary>
    [Fact]
    public async Task AuthorizationCodeGrant_AccessTokenHasUserClaims()
    {
        using var factory = new IdentityApplicationFactory();
        var (user, code) = await RegisterSsoUserWithAuthorizationCodeAsync(factory);
        var organizationUser = Assert.Single(await factory.Services
            .GetRequiredService<IOrganizationUserRepository>().GetManyByUserAsync(user.Id));

        var context = await PostAuthorizationCodeAsync(factory, code);

        using var body = await AssertDefaultTokenBodyAsync(context);
        var claims = ReadPayload(AssertAccessTokenExists(body.RootElement));
        Assert.Equal(user.Id.ToString(), claims.GetProperty(JwtClaimTypes.Subject).GetString());
        Assert.Equal(user.Email, claims.GetProperty(JwtClaimTypes.Email).GetString());
        Assert.Equal(user.SecurityStamp, claims.GetProperty(Claims.SecurityStamp).GetString());
        Assert.False(claims.GetProperty(Claims.Premium).GetBoolean());
        Assert.True(PayloadContainsClaimValue(claims, Claims.OrganizationUser,
            organizationUser.OrganizationId.ToString()));
        var authenticationMethods = claims.GetProperty(JwtClaimTypes.AuthenticationMethod);
        Assert.Equal(JsonValueKind.Array, authenticationMethods.ValueKind);
        Assert.Equal(new[] { "external" }, authenticationMethods.EnumerateArray().Select(e => e.GetString()));
        Assert.Equal("sso", claims.GetProperty(JwtClaimTypes.IdentityProvider).GetString());
    }
}
