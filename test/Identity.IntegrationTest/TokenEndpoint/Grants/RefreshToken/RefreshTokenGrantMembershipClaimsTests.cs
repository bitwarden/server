using Bit.Core.Auth.Identity;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Identity.IntegrationTest.Helpers;
using Bit.IntegrationTestCommon.Factories;
using Xunit;
using static Bit.Identity.IntegrationTest.Helpers.AccessTokenAssertions;
using static Bit.Identity.IntegrationTest.Helpers.RefreshTokenGrantTestHelpers;
using static Bit.Identity.IntegrationTest.Helpers.TokenResponseAssertions;

#nullable enable

namespace Bit.Identity.IntegrationTest.TokenEndpoint.Grants.RefreshToken;

/// <summary>
/// Integration tests for the membership claims on a refreshed access token. A refresh rebuilds these claims from the
/// database, so a change made after the login appears in the new access token.
/// </summary>
public class RefreshTokenGrantMembershipClaimsTests
{
    /// <summary>
    /// A member who is removed from the organization after the login gets no organization claim on a refreshed access
    /// token.
    /// </summary>
    [Fact]
    public async Task RefreshToken_ReflectsCurrentOrganizationMembership()
    {
        using var factory = new IdentityApplicationFactory();
        var (membership, accessToken, refreshToken) = await RegisterMemberAndLoginAsync(
            factory, OrganizationUserType.Owner);
        Assert.True(PayloadContainsClaimValue(
            ReadPayload(accessToken), Claims.OrganizationOwner, membership.OrganizationId.ToString()));

        await factory.Services.GetRequiredService<IOrganizationUserRepository>().DeleteAsync(membership);

        var context = await PostRefreshTokenAsync(factory, refreshToken);

        using var body = await AssertDefaultTokenBodyAsync(context);
        var refreshedClaims = ReadPayload(AssertAccessTokenExists(body.RootElement));
        Assert.False(refreshedClaims.TryGetProperty(Claims.OrganizationOwner, out _));
    }

    /// <summary>
    /// A Custom member whose permission is removed after the login gets no claim for that permission on a refreshed
    /// access token.
    /// </summary>
    [Fact]
    public async Task RefreshToken_ReflectsRemovedCustomPermission()
    {
        using var factory = new IdentityApplicationFactory();
        var (membership, accessToken, refreshToken) = await RegisterMemberAndLoginAsync(
            factory, OrganizationUserType.Custom, permissions: new Permissions { ManageUsers = true });
        Assert.True(PayloadContainsClaimValue(
            ReadPayload(accessToken), Claims.CustomPermissions.ManageUsers, membership.OrganizationId.ToString()));

        membership.SetPermissions(new Permissions { ManageUsers = false });
        await factory.Services.GetRequiredService<IOrganizationUserRepository>().ReplaceAsync(membership);

        var context = await PostRefreshTokenAsync(factory, refreshToken);

        using var body = await AssertDefaultTokenBodyAsync(context);
        var refreshedClaims = ReadPayload(AssertAccessTokenExists(body.RootElement));
        Assert.False(refreshedClaims.TryGetProperty(Claims.CustomPermissions.ManageUsers, out _));
    }

    /// <summary>
    /// A member whose Secrets Manager access is removed after the login gets no Secrets Manager claim on a refreshed
    /// access token.
    /// </summary>
    [Fact]
    public async Task RefreshToken_ReflectsRemovedSecretsManagerAccess()
    {
        using var factory = new IdentityApplicationFactory();
        var (membership, accessToken, refreshToken) = await RegisterMemberAndLoginAsync(
            factory, OrganizationUserType.User, accessSecretsManager: true);
        Assert.True(PayloadContainsClaimValue(
            ReadPayload(accessToken), Claims.SecretsManagerAccess, membership.OrganizationId.ToString()));

        membership.AccessSecretsManager = false;
        await factory.Services.GetRequiredService<IOrganizationUserRepository>().ReplaceAsync(membership);

        var context = await PostRefreshTokenAsync(factory, refreshToken);

        using var body = await AssertDefaultTokenBodyAsync(context);
        var refreshedClaims = ReadPayload(AssertAccessTokenExists(body.RootElement));
        Assert.False(refreshedClaims.TryGetProperty(Claims.SecretsManagerAccess, out _));
    }

    /// <summary>
    /// Registers the default password-grant user, makes the user a confirmed member of a new organization, and logs in
    /// with the password grant on the web client.
    /// </summary>
    private static async Task<(OrganizationUser Membership, string AccessToken, string RefreshToken)>
        RegisterMemberAndLoginAsync(
            IdentityApplicationFactory factory,
            OrganizationUserType organizationUserType,
            Permissions? permissions = null,
            bool accessSecretsManager = false)
    {
        await PasswordGrantTestHelpers.RegisterUserAsync(factory);
        var user = await factory.Services.GetRequiredService<IUserRepository>()
            .GetByEmailAsync(PasswordGrantTestHelpers.DefaultUsername);

        // The membership exists before the login, so the login token carries the membership claims.
        var membership = await OrganizationTestHelpers.CreateOrganizationWithConfirmedMemberAsync(
            factory, user!.Id, organizationUserType, permissions, accessSecretsManager);

        var (accessToken, refreshToken) = await factory.TokenFromPasswordAsync(
            PasswordGrantTestHelpers.DefaultUsername, PasswordGrantTestHelpers.DefaultPassword);
        return (membership, accessToken, refreshToken);
    }
}
