using System.Text.Json;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.Auth.Identity;
using Bit.Core.Enums;
using Bit.IntegrationTestCommon.Factories;
using Duende.IdentityModel;
using Xunit;
using static Bit.Identity.IntegrationTest.Helpers.AccessTokenAssertions;
using static Bit.Identity.IntegrationTest.Helpers.OrganizationTestHelpers;
using static Bit.Identity.IntegrationTest.Helpers.PasswordGrantTestHelpers;
using static Bit.Identity.IntegrationTest.Helpers.ProviderTestHelpers;
using static Bit.Identity.IntegrationTest.Helpers.TokenResponseAssertions;
using static Bit.Identity.IntegrationTest.Helpers.UserTestHelpers;

#nullable enable

namespace Bit.Identity.IntegrationTest.TokenEndpoint.Grants.Password;

/// <summary>
/// Integration tests for the user claims on the access token that a password login issues.
/// </summary>
public class PasswordGrantUserClaimsTests
{
    private static readonly string[] _organizationRoleClaimTypes =
    [
        Claims.OrganizationOwner, Claims.OrganizationAdmin, Claims.OrganizationUser, Claims.OrganizationCustom,
    ];

    private static readonly string[] _providerRoleClaimTypes = [Claims.ProviderAdmin, Claims.ProviderServiceUser];

    /// <summary>
    /// The access token carries the account claims of the user as stored in the database, the login method, and
    /// Bitwarden as the identity provider.
    /// </summary>
    [Fact]
    public async Task PasswordGrant_UserWithNameAndPremium_AccessTokenHasUserClaims()
    {
        const string name = "Login User";
        using var factory = new IdentityApplicationFactory();
        var user = await RegisterUserAsync(factory);
        // Registration does not set premium and sets another name, so these claims can only come from the stored user.
        await UpdateUserAsync(factory, user.Id, u =>
        {
            u.Premium = true;
            u.Name = name;
        });

        var context = await factory.ContextFromPasswordAsync(DefaultUsername, DefaultPassword);

        using var body = await AssertDefaultTokenBodyAsync(context);
        var claims = ReadPayload(AssertAccessTokenExists(body.RootElement));
        Assert.Equal(user.Id.ToString(), claims.GetProperty(JwtClaimTypes.Subject).GetString());
        Assert.Equal(DefaultUsername, claims.GetProperty(JwtClaimTypes.Email).GetString());
        Assert.Equal(user.EmailVerified, claims.GetProperty(JwtClaimTypes.EmailVerified).GetBoolean());
        Assert.Equal(name, claims.GetProperty(JwtClaimTypes.Name).GetString());
        Assert.True(claims.GetProperty(Claims.Premium).GetBoolean());
        Assert.Equal(user.SecurityStamp, claims.GetProperty(Claims.SecurityStamp).GetString());
        var authenticationMethods = claims.GetProperty(JwtClaimTypes.AuthenticationMethod);
        Assert.Equal(JsonValueKind.Array, authenticationMethods.ValueKind);
        Assert.Equal(new[] { "Application" }, authenticationMethods.EnumerateArray().Select(e => e.GetString()));
        Assert.Equal("bitwarden", claims.GetProperty(JwtClaimTypes.IdentityProvider).GetString());
    }

    /// <summary>
    /// A user without a name gets no name claim, and a user without premium gets a premium claim of false.
    /// </summary>
    [Fact]
    public async Task PasswordGrant_UserWithoutNameOrPremium_AccessTokenHasNoNameAndPremiumFalse()
    {
        using var factory = new IdentityApplicationFactory();
        var user = await RegisterUserAsync(factory);
        Assert.False(user.Premium);
        await UpdateUserAsync(factory, user.Id, u => u.Name = null);

        var context = await factory.ContextFromPasswordAsync(DefaultUsername, DefaultPassword);

        using var body = await AssertDefaultTokenBodyAsync(context);
        var claims = ReadPayload(AssertAccessTokenExists(body.RootElement));
        Assert.False(claims.TryGetProperty(JwtClaimTypes.Name, out _));
        Assert.False(claims.GetProperty(Claims.Premium).GetBoolean());
    }

    public static TheoryData<OrganizationUserType, string> OrganizationRoleClaims => new()
    {
        { OrganizationUserType.Owner, Claims.OrganizationOwner },
        { OrganizationUserType.Admin, Claims.OrganizationAdmin },
        { OrganizationUserType.User, Claims.OrganizationUser },
        { OrganizationUserType.Custom, Claims.OrganizationCustom },
    };

    /// <summary>
    /// A confirmed member of an organization gets the claim for their role in it, and no claim for another role.
    /// </summary>
    [Theory]
    [MemberData(nameof(OrganizationRoleClaims))]
    public async Task PasswordGrant_OrganizationMember_AccessTokenHasRoleClaim(
        OrganizationUserType organizationUserType, string expectedClaimType)
    {
        using var factory = new IdentityApplicationFactory();
        var user = await RegisterUserAsync(factory);
        var membership = await CreateOrganizationWithConfirmedMemberAsync(factory, user.Id, organizationUserType);

        var context = await factory.ContextFromPasswordAsync(DefaultUsername, DefaultPassword);

        using var body = await AssertDefaultTokenBodyAsync(context);
        var claims = ReadPayload(AssertAccessTokenExists(body.RootElement));
        var organizationId = membership.OrganizationId.ToString();
        foreach (var claimType in _organizationRoleClaimTypes)
        {
            Assert.Equal(claimType == expectedClaimType,
                PayloadContainsClaimValue(claims, claimType, organizationId));
        }
    }

    /// <summary>
    /// A confirmed member with PAM access in an organization that uses PAM gets the PAM access claim.
    /// </summary>
    [Fact]
    public async Task PasswordGrant_PamMember_AccessTokenHasPamAccessClaim()
    {
        using var factory = new IdentityApplicationFactory();
        var user = await RegisterUserAsync(factory);
        var membership = await CreateOrganizationWithConfirmedMemberAsync(
            factory, user.Id, OrganizationUserType.User, accessPam: true);

        var context = await factory.ContextFromPasswordAsync(DefaultUsername, DefaultPassword);

        using var body = await AssertDefaultTokenBodyAsync(context);
        var claims = ReadPayload(AssertAccessTokenExists(body.RootElement));
        Assert.True(PayloadContainsClaimValue(claims, Claims.PamAccess, membership.OrganizationId.ToString()));
    }

    public static TheoryData<ProviderUserType, string> ProviderRoleClaims => new()
    {
        { ProviderUserType.ProviderAdmin, Claims.ProviderAdmin },
        { ProviderUserType.ServiceUser, Claims.ProviderServiceUser },
    };

    /// <summary>
    /// A confirmed member of a provider gets the claim for their role in it, and no claim for the other role.
    /// </summary>
    [Theory]
    [MemberData(nameof(ProviderRoleClaims))]
    public async Task PasswordGrant_ProviderMember_AccessTokenHasRoleClaim(
        ProviderUserType providerUserType, string expectedClaimType)
    {
        using var factory = new IdentityApplicationFactory();
        var user = await RegisterUserAsync(factory);
        var membership = await CreateProviderWithConfirmedMemberAsync(factory, user.Id, providerUserType);

        var context = await factory.ContextFromPasswordAsync(DefaultUsername, DefaultPassword);

        using var body = await AssertDefaultTokenBodyAsync(context);
        var claims = ReadPayload(AssertAccessTokenExists(body.RootElement));
        var providerId = membership.ProviderId.ToString();
        foreach (var claimType in _providerRoleClaimTypes)
        {
            Assert.Equal(claimType == expectedClaimType, PayloadContainsClaimValue(claims, claimType, providerId));
        }
    }
}
