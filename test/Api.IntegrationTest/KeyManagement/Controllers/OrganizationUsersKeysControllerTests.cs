using System.Net;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Api.KeyManagement.Models.Requests;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Api.IntegrationTest.KeyManagement.Controllers;

public class OrganizationUsersKeysControllerTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private const string _userKeyId = "0123456789abcdef0123456789abcdef";
    private const string _v1AccountRecoveryKey =
        "2.AOs41Hd8OQiCPXjyJKCiDA==|O6OHgt2U2hJGBSNGnimJmg==|iD33s8B69C8JhYYhSa4V1tArjvLr8eEaGqOV7BRo5Jk=";
    private const string _v2AccountRecoveryKey =
        "2.06CDSJjTZaigYHUuswIq5A==|trxgZl2RCkYrrmCvGE9WNA==|w5p05eI5wsaYeSyWtsAPvBX63vj798kIMxBTfSB0BQg=";
    private const string _v2UpgradeToken =
        """{"WrappedUserKey1":"7.AOs41Hd8OQiCPXjyJKCiDA==","WrappedUserKey2":"7.Mi1iaXR3YXJkZW4tZGF0YQo="}""";

    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;
    private readonly IUserRepository _userRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;

    private string _ownerEmail = null!;
    private Organization _organization = null!;

    public OrganizationUsersKeysControllerTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
        _userRepository = _factory.GetService<IUserRepository>();
        _organizationUserRepository = _factory.GetService<IOrganizationUserRepository>();
    }

    public async Task InitializeAsync()
    {
        _ownerEmail = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(_ownerEmail);

        var (organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory,
            PlanType.EnterpriseAnnually, _ownerEmail, passwordManagerSeats: 10,
            paymentMethod: PaymentMethodType.Card);
        _organization = organization;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GetPendingV2UpgradesAsync_MemberWithoutAccountRecoveryPermission_Forbidden()
    {
        // Arrange
        var memberEmail = await CreateMemberAsync(OrganizationUserType.User);
        await _loginHelper.LoginAsync(memberEmail);

        // Act
        var response = await _client.GetAsync(PendingUpgradesUri());

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PostV2UpgradesAsync_MemberWithoutAccountRecoveryPermission_ForbiddenAndKeyIsUnchanged()
    {
        // Arrange
        var memberEmail = await CreateMemberAsync(OrganizationUserType.User);
        var organizationUser = await GivenAPendingUpgradeAsync(memberEmail);
        await _loginHelper.LoginAsync(memberEmail);

        // Act
        var response = await _client.PostAsJsonAsync(UpgradesUri(), RequestFor(organizationUser.Id, _userKeyId));

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var written = await _organizationUserRepository.GetByIdAsync(organizationUser.Id);
        Assert.NotNull(written);
        Assert.Equal(_v1AccountRecoveryKey, written.ResetPasswordKey);
        Assert.Equal(_v2UpgradeToken, written.V2UpgradeToken);
    }

    [Fact]
    public async Task GetPendingV2UpgradesAsync_CustomWithAccountRecoveryButNotManageUsers_Forbidden()
    {
        // Arrange - ManageUsers is also required, because the admin must read the organization's private key from
        // GET organizations/{orgId}/private-key to unwrap anything
        var memberEmail = await CreateCustomMemberAsync(new Permissions
        {
            ManageResetPassword = true,
            ManageUsers = false
        });
        await _loginHelper.LoginAsync(memberEmail);

        // Act
        var response = await _client.GetAsync(PendingUpgradesUri());

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetPendingV2UpgradesAsync_CustomWithManageUsersButNotAccountRecovery_Forbidden()
    {
        // Arrange - neither permission alone grants access to the endpoint
        var memberEmail = await CreateCustomMemberAsync(new Permissions
        {
            ManageResetPassword = false,
            ManageUsers = true
        });
        await _loginHelper.LoginAsync(memberEmail);

        // Act
        var response = await _client.GetAsync(PendingUpgradesUri());

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetPendingV2UpgradesAsync_CustomWithBothPermissions_Succeeds()
    {
        // Arrange
        var memberEmail = await CreateCustomMemberAsync(new Permissions
        {
            ManageResetPassword = true,
            ManageUsers = true
        });
        await _loginHelper.LoginAsync(memberEmail);

        // Act
        var response = await _client.GetAsync(PendingUpgradesUri());

        // Assert
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task V2Upgrade_RoundTrip_ReplacesTheKeyAndEmptiesThePendingList()
    {
        // Arrange
        var memberEmail = await CreateMemberAsync(OrganizationUserType.User);
        var organizationUser = await GivenAPendingUpgradeAsync(memberEmail);
        await _loginHelper.LoginAsync(_ownerEmail);

        // Act - the admin reads the pending upgrade
        var pending = await _client.GetAsync(PendingUpgradesUri());
        pending.EnsureSuccessStatusCode();
        var pendingBody = await pending.Content.ReadAsStringAsync();

        // Assert - the response contains the token and the key id
        Assert.Contains(organizationUser.Id.ToString(), pendingBody);
        Assert.Contains(_userKeyId, pendingBody);
        Assert.Contains("wrappedUserKey2", pendingBody);

        // Act - the admin posts the re-wrapped key
        var upgrade = await _client.PostAsJsonAsync(UpgradesUri(), RequestFor(organizationUser.Id, _userKeyId));
        upgrade.EnsureSuccessStatusCode();

        // Assert - the key is replaced and the token is cleared
        var written = await _organizationUserRepository.GetByIdAsync(organizationUser.Id);
        Assert.NotNull(written);
        Assert.Equal(_v2AccountRecoveryKey, written.ResetPasswordKey);
        Assert.Null(written.V2UpgradeToken);

        var pendingAfter = await _client.GetAsync(PendingUpgradesUri());
        pendingAfter.EnsureSuccessStatusCode();
        Assert.DoesNotContain(organizationUser.Id.ToString(), await pendingAfter.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task V2Upgrade_OwnerActingOnTheirOwnMembership_ReplacesTheKeyAndEmptiesThePendingList()
    {
        // Arrange - only an Owner can recover an Owner, so a sole Owner has to complete their own upgrade
        var organizationUser = await GivenAPendingUpgradeAsync(_ownerEmail);
        await _loginHelper.LoginAsync(_ownerEmail);

        // Act - the owner reads their own pending upgrade
        var pending = await _client.GetAsync(PendingUpgradesUri());
        pending.EnsureSuccessStatusCode();

        // Assert - the owner's own membership is listed
        Assert.Contains(organizationUser.Id.ToString(), await pending.Content.ReadAsStringAsync());

        // Act - the owner posts their own re-wrapped key
        var upgrade = await _client.PostAsJsonAsync(UpgradesUri(), RequestFor(organizationUser.Id, _userKeyId));
        upgrade.EnsureSuccessStatusCode();

        // Assert - the key is replaced and the token is cleared
        var written = await _organizationUserRepository.GetByIdAsync(organizationUser.Id);
        Assert.NotNull(written);
        Assert.Equal(_v2AccountRecoveryKey, written.ResetPasswordKey);
        Assert.Null(written.V2UpgradeToken);

        var pendingAfter = await _client.GetAsync(PendingUpgradesUri());
        pendingAfter.EnsureSuccessStatusCode();
        Assert.DoesNotContain(organizationUser.Id.ToString(), await pendingAfter.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task PostV2UpgradesAsync_NoAccountRecoveryKey_UnenrollsTheMemberAndEmptiesThePendingList()
    {
        // Arrange - the upgrade cannot be completed, so the admin unenrolls the member instead
        var memberEmail = await CreateMemberAsync(OrganizationUserType.User);
        var organizationUser = await GivenAPendingUpgradeAsync(memberEmail);
        await _loginHelper.LoginAsync(_ownerEmail);

        // Act
        var response = await _client.PostAsJsonAsync(UpgradesUri(),
            RequestFor(organizationUser.Id, _userKeyId, accountRecoveryKey: null));

        // Assert
        response.EnsureSuccessStatusCode();

        var written = await _organizationUserRepository.GetByIdAsync(organizationUser.Id);
        Assert.NotNull(written);
        Assert.Null(written.ResetPasswordKey);
        Assert.Null(written.V2UpgradeToken);

        var pendingAfter = await _client.GetAsync(PendingUpgradesUri());
        pendingAfter.EnsureSuccessStatusCode();
        Assert.DoesNotContain(organizationUser.Id.ToString(), await pendingAfter.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task PostV2UpgradesAsync_StaleUserKeyId_SucceedsAndLeavesTheUpgradePending()
    {
        // Arrange - the member rotated again, so the re-wrapped key uses a user key they no longer hold
        var memberEmail = await CreateMemberAsync(OrganizationUserType.User);
        var organizationUser = await GivenAPendingUpgradeAsync(memberEmail);
        await _loginHelper.LoginAsync(_ownerEmail);

        // Act
        var response = await _client.PostAsJsonAsync(UpgradesUri(),
            RequestFor(organizationUser.Id, "fedcba9876543210fedcba9876543210"));

        // Assert - the upgrade is skipped rather than rejected, and stays pending for the admin to read again
        response.EnsureSuccessStatusCode();

        var written = await _organizationUserRepository.GetByIdAsync(organizationUser.Id);
        Assert.NotNull(written);
        Assert.Equal(_v1AccountRecoveryKey, written.ResetPasswordKey);
        Assert.Equal(_v2UpgradeToken, written.V2UpgradeToken);

        var pendingAfter = await _client.GetAsync(PendingUpgradesUri());
        pendingAfter.EnsureSuccessStatusCode();
        Assert.Contains(organizationUser.Id.ToString(), await pendingAfter.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task PostV2UpgradesAsync_MalformedUserKeyId_BadRequest()
    {
        // Arrange
        var memberEmail = await CreateMemberAsync(OrganizationUserType.User);
        var organizationUser = await GivenAPendingUpgradeAsync(memberEmail);
        await _loginHelper.LoginAsync(_ownerEmail);

        // Act
        var response = await _client.PostAsJsonAsync(UpgradesUri(), new
        {
            Upgrades = new[]
            {
                new
                {
                    OrganizationUserId = organizationUser.Id,
                    UserKeyId = "not-a-key-id",
                    AccountRecoveryKey = _v2AccountRecoveryKey
                }
            }
        });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private string PendingUpgradesUri() =>
        $"/organizations/{_organization.Id}/users/keys/pending-v2-upgrades";

    private string UpgradesUri() => $"/organizations/{_organization.Id}/users/keys/v2-upgrades";

    private static OrganizationUserV2UpgradesRequestModel RequestFor(Guid organizationUserId, string userKeyId,
        string? accountRecoveryKey = _v2AccountRecoveryKey) =>
        new()
        {
            Upgrades =
            [
                new OrganizationUserV2UpgradeRequestModel
                {
                    OrganizationUserId = organizationUserId,
                    UserKeyId = userKeyId,
                    AccountRecoveryKey = accountRecoveryKey
                }
            ]
        };

    private async Task<string> CreateMemberAsync(OrganizationUserType type)
    {
        var email = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(email);
        await OrganizationTestHelpers.CreateUserAsync(_factory, _organization.Id, email, type);
        return email;
    }

    private async Task<string> CreateCustomMemberAsync(Permissions permissions)
    {
        var email = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(email);
        await OrganizationTestHelpers.CreateUserAsync(_factory, _organization.Id, email,
            OrganizationUserType.Custom, permissions: permissions);
        return email;
    }

    /// <summary>
    /// Sets up a membership in the state left by a V1 to V2 upgrade rotation: an account recovery key that still
    /// wraps the V1 user key, a V2 upgrade token, and a user row with the new key id.
    /// </summary>
    private async Task<OrganizationUser> GivenAPendingUpgradeAsync(string memberEmail)
    {
        var user = await _userRepository.GetByEmailAsync(memberEmail);
        Assert.NotNull(user);
        user.UserKeyId = _userKeyId;
        await _userRepository.ReplaceAsync(user);

        var organizationUser = await _organizationUserRepository.GetByOrganizationAsync(_organization.Id, user.Id);
        Assert.NotNull(organizationUser);
        organizationUser.ResetPasswordKey = _v1AccountRecoveryKey;
        organizationUser.V2UpgradeToken = _v2UpgradeToken;
        await _organizationUserRepository.ReplaceAsync(organizationUser);

        return organizationUser;
    }
}
