using System.Text;
using System.Text.Json;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Identity;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Test.Auth.AutoFixture;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;
using static Bit.Identity.IntegrationTest.Helpers.TokenEndpointTestHelpers;
using static Bit.Identity.IntegrationTest.Helpers.TokenResponseAssertions;

namespace Bit.Identity.IntegrationTest.TokenEndpoint.Grants.RefreshToken;

/// <summary>
/// Integration tests for the <c>refresh_token</c> grant: redeeming a refresh token at <c>/connect/token</c> to
/// obtain a new access token. On this flow IdentityServer re-invokes the Profile Service, which rebuilds the
/// user's membership claims from the database, so a refreshed token always reflects the member's current state.
/// </summary>
public class RefreshTokenGrantTests : IClassFixture<IdentityApplicationFactory>
{
    private readonly IdentityApplicationFactory _factory;

    public RefreshTokenGrantTests(IdentityApplicationFactory factory)
    {
        _factory = factory;
        InitializeSharedFactory(_factory);
    }

    [Theory, BitAutoData, RegisterFinishRequestModelCustomize]
    public async Task RefreshToken_Success(RegisterFinishRequestModel requestModel)
    {
        requestModel.UserAsymmetricKeys = TEST_ACCOUNT_KEYS;
        var localFactory = new IdentityApplicationFactory();

        await localFactory.RegisterNewIdentityFactoryUserAsync(requestModel);

        var (_, refreshToken) = await localFactory.TokenFromPasswordAsync(
            requestModel.Email, requestModel.MasterPasswordHash);

        using var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "grant_type", "refresh_token" },
            { "client_id", "web" },
            { "refresh_token", refreshToken },
        });
        var context = await localFactory.Server.PostAsync("/connect/token", formContent);

        using var body = await AssertDefaultTokenBodyAsync(context);
        AssertRefreshTokenExists(body.RootElement);
    }

    /// <summary>
    /// Membership claims are rebuilt from the database on every token issuance, so a refreshed access token
    /// reflects the member's current organization membership. When a user is removed from an organization, the
    /// organization claim present on the earlier token is not re-issued on refresh.
    /// </summary>
    [Theory, BitAutoData, RegisterFinishRequestModelCustomize]
    public async Task RefreshToken_ReflectsCurrentOrganizationMembership(
        RegisterFinishRequestModel requestModel)
    {
        requestModel.UserAsymmetricKeys = TEST_ACCOUNT_KEYS;
        var localFactory = new IdentityApplicationFactory();

        var user = await localFactory.RegisterNewIdentityFactoryUserAsync(requestModel);

        // Give the user an organization membership so the issued token carries an organization claim.
        var organizationId = Guid.NewGuid();
        await CreateOrganizationMembershipAsync(localFactory, organizationId, user.Email, OrganizationUserType.Owner);

        // Initial password login: the access token reflects the current (present) membership.
        var (accessToken, refreshToken) = await localFactory.TokenFromPasswordAsync(
            requestModel.Email, requestModel.MasterPasswordHash);
        var initialClaims = ReadJwtPayload(accessToken);
        Assert.True(JwtPayloadContainsClaimValue(initialClaims, Claims.OrganizationOwner, organizationId.ToString()));

        // The user is removed from the organization.
        var organizationUserRepository = localFactory.Services.GetRequiredService<IOrganizationUserRepository>();
        var membership = await organizationUserRepository.GetByOrganizationAsync(organizationId, user.Id);
        Assert.NotNull(membership);
        await organizationUserRepository.DeleteAsync(membership);

        // Refresh the token: the rebuilt token reflects current membership, so the organization claim is gone.
        using var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "grant_type", "refresh_token" },
            { "client_id", "web" },
            { "refresh_token", refreshToken },
        });
        var context = await localFactory.Server.PostAsync("/connect/token", formContent);

        using var body = await AssertDefaultTokenBodyAsync(context);
        var refreshedAccessToken = AssertAccessTokenExists(body.RootElement);
        var refreshedClaims = ReadJwtPayload(refreshedAccessToken);
        Assert.False(refreshedClaims.TryGetProperty(Claims.OrganizationOwner, out _));
    }

    /// <summary>
    /// Membership claims are rebuilt from the database on every token issuance. A Custom-role member's granted
    /// permissions are carried as individual claims; when a permission is removed, the corresponding claim must
    /// not be re-issued on refresh.
    /// </summary>
    [Theory, BitAutoData, RegisterFinishRequestModelCustomize]
    public async Task RefreshToken_ReflectsRemovedCustomPermission(
        RegisterFinishRequestModel requestModel)
    {
        requestModel.UserAsymmetricKeys = TEST_ACCOUNT_KEYS;
        var localFactory = new IdentityApplicationFactory();

        var user = await localFactory.RegisterNewIdentityFactoryUserAsync(requestModel);

        // Give the user a Custom membership with the ManageUsers permission granted.
        var organizationId = Guid.NewGuid();
        await CreateOrganizationMembershipAsync(localFactory, organizationId, user.Email, OrganizationUserType.Custom,
            permissions: new Permissions { ManageUsers = true });

        // Initial password login: the access token reflects the granted permission.
        var (accessToken, refreshToken) = await localFactory.TokenFromPasswordAsync(
            requestModel.Email, requestModel.MasterPasswordHash);
        var initialClaims = ReadJwtPayload(accessToken);
        Assert.True(JwtPayloadContainsClaimValue(initialClaims, Claims.CustomPermissions.ManageUsers, organizationId.ToString()));

        // The ManageUsers permission is removed.
        var organizationUserRepository = localFactory.Services.GetRequiredService<IOrganizationUserRepository>();
        var membership = await organizationUserRepository.GetByOrganizationAsync(organizationId, user.Id);
        Assert.NotNull(membership);
        membership.SetPermissions(new Permissions { ManageUsers = false });
        await organizationUserRepository.ReplaceAsync(membership);

        // Refresh the token: the rebuilt token reflects current permissions, so the ManageUsers claim is gone.
        using var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "grant_type", "refresh_token" },
            { "client_id", "web" },
            { "refresh_token", refreshToken },
        });
        var context = await localFactory.Server.PostAsync("/connect/token", formContent);

        using var body = await AssertDefaultTokenBodyAsync(context);
        var refreshedAccessToken = AssertAccessTokenExists(body.RootElement);
        var refreshedClaims = ReadJwtPayload(refreshedAccessToken);
        Assert.False(refreshedClaims.TryGetProperty(Claims.CustomPermissions.ManageUsers, out _));
    }

    /// <summary>
    /// Membership claims are rebuilt from the database on every token issuance. Secrets Manager access is granted
    /// per-organization; when access is removed, the corresponding claim must not be re-issued on refresh.
    /// </summary>
    [Theory, BitAutoData, RegisterFinishRequestModelCustomize]
    public async Task RefreshToken_ReflectsRemovedSecretsManagerAccess(
        RegisterFinishRequestModel requestModel)
    {
        requestModel.UserAsymmetricKeys = TEST_ACCOUNT_KEYS;
        var localFactory = new IdentityApplicationFactory();

        var user = await localFactory.RegisterNewIdentityFactoryUserAsync(requestModel);

        // Give the user Secrets Manager access on the organization.
        var organizationId = Guid.NewGuid();
        await CreateOrganizationMembershipAsync(localFactory, organizationId, user.Email, OrganizationUserType.User,
            accessSecretsManager: true);

        // Initial password login: the access token reflects Secrets Manager access.
        var (accessToken, refreshToken) = await localFactory.TokenFromPasswordAsync(
            requestModel.Email, requestModel.MasterPasswordHash);
        var initialClaims = ReadJwtPayload(accessToken);
        Assert.True(JwtPayloadContainsClaimValue(initialClaims, Claims.SecretsManagerAccess, organizationId.ToString()));

        // Secrets Manager access is removed.
        var organizationUserRepository = localFactory.Services.GetRequiredService<IOrganizationUserRepository>();
        var membership = await organizationUserRepository.GetByOrganizationAsync(organizationId, user.Id);
        Assert.NotNull(membership);
        membership.AccessSecretsManager = false;
        await organizationUserRepository.ReplaceAsync(membership);

        // Refresh the token: the rebuilt token reflects current access, so the Secrets Manager claim is gone.
        using var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "grant_type", "refresh_token" },
            { "client_id", "web" },
            { "refresh_token", refreshToken },
        });
        var context = await localFactory.Server.PostAsync("/connect/token", formContent);

        using var body = await AssertDefaultTokenBodyAsync(context);
        var refreshedAccessToken = AssertAccessTokenExists(body.RootElement);
        var refreshedClaims = ReadJwtPayload(refreshedAccessToken);
        Assert.False(refreshedClaims.TryGetProperty(Claims.SecretsManagerAccess, out _));
    }

    private static async Task CreateOrganizationMembershipAsync(
        IdentityApplicationFactory localFactory,
        Guid organizationId,
        string username,
        OrganizationUserType organizationUserType,
        Permissions permissions = null,
        bool accessSecretsManager = false)
    {
        var userRepository = localFactory.Services.GetRequiredService<IUserRepository>();
        var organizationRepository = localFactory.Services.GetRequiredService<IOrganizationRepository>();
        var organizationUserRepository = localFactory.Services.GetRequiredService<IOrganizationUserRepository>();

        await organizationRepository.CreateAsync(new Organization
        {
            Id = organizationId,
            Name = $"Org Name | {organizationId}",
            Enabled = true,
            Plan = "Enterprise",
            BillingEmail = $"billing-email+{organizationId}@example.com",
            UseSecretsManager = accessSecretsManager,
        });

        var user = await userRepository.GetByEmailAsync(username);
        var organizationUser = new OrganizationUser
        {
            OrganizationId = organizationId,
            UserId = user.Id,
            Status = OrganizationUserStatusType.Confirmed,
            Type = organizationUserType,
            AccessSecretsManager = accessSecretsManager,
        };
        if (permissions != null)
        {
            organizationUser.SetPermissions(permissions);
        }

        await organizationUserRepository.CreateAsync(organizationUser);
    }

    /// <summary>
    /// Decodes the payload segment of a JWT into its JSON claims for inspection.
    /// </summary>
    private static JsonElement ReadJwtPayload(string jwt)
    {
        var payload = jwt.Split('.')[1].Replace('-', '+').Replace('_', '/');
        payload += (payload.Length % 4) switch
        {
            2 => "==",
            3 => "=",
            _ => string.Empty
        };

        var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    /// <summary>
    /// A claim may be serialized on the token as a single value or as an array of values, so both shapes are checked.
    /// </summary>
    private static bool JwtPayloadContainsClaimValue(JsonElement payload, string claimType, string value)
    {
        if (!payload.TryGetProperty(claimType, out var claim))
        {
            return false;
        }

        return claim.ValueKind == JsonValueKind.Array
            ? claim.EnumerateArray().Any(element => element.GetString() == value)
            : claim.GetString() == value;
    }
}
