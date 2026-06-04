using System.Net;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Api.Models.Response;
using Bit.Core;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Repositories;
using Bit.Core.Services;
using NSubstitute;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Controllers;

public class OrganizationUserControllerBulkAutoConfirmTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private const string _mockEncryptedString =
        "2.AOs41Hd8OQiCPXjyJKCiDA==|O6OHgt2U2hJGBSNGnimJmg==|iD33s8B69C8JhYYhSa4V1tArjvLr8eEaGqOV7BRo5Jk=";

    private const string _mockEncryptedString2 =
        "2.BPt52Ie9RRjDQmyKLCjBEB==|P7PHhu3V3iIHCTOHgu3Knh==|jE44t9C7AKdIZiJtb5WW2eEbRQs42DfRpAH1cSo6Kmq=";

    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;

    private string _ownerEmail = null!;

    public OrganizationUserControllerBulkAutoConfirmTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _factory.SubstituteService<IFeatureService>(featureService =>
        {
            featureService
                .IsEnabled(FeatureFlagKeys.BulkAutoConfirmOnLogin)
                .Returns(true);
        });
        _factory.SubstituteService<IApplicationCacheService>(cacheService =>
        {
            cacheService
                .GetOrganizationAbilityAsync(Arg.Any<Guid>())
                .Returns(new OrganizationAbility { UseAutomaticUserConfirmation = true });
        });
        _client = _factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
    }

    public async Task InitializeAsync()
    {
        _ownerEmail = $"bulk-auto-confirm-owner-{Guid.NewGuid()}@example.com";
        await _factory.LoginWithNewAccount(_ownerEmail);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GetPendingAutoConfirm_WhenOwnerRequests_ReturnsPendingUsers()
    {
        var (organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory,
            plan: PlanType.EnterpriseAnnually,
            ownerEmail: _ownerEmail,
            passwordManagerSeats: 10,
            paymentMethod: PaymentMethodType.Card);

        organization.UseAutomaticUserConfirmation = true;
        await _factory.GetService<IOrganizationRepository>().UpsertAsync(organization);

        await _factory.GetService<IPolicyRepository>().CreateAsync(new Core.AdminConsole.Entities.Policy
        {
            OrganizationId = organization.Id,
            Type = PolicyType.AutomaticUserConfirmation,
            Enabled = true
        });

        // An Accepted User-role member — should appear in the results.
        var pendingEmail = $"pending-user-{Guid.NewGuid()}@example.com";
        await _factory.LoginWithNewAccount(pendingEmail);
        var pendingOrgUser = await OrganizationTestHelpers.CreateUserAsync(_factory, organization.Id, pendingEmail,
            OrganizationUserType.User, userStatusType: OrganizationUserStatusType.Accepted);

        // A Confirmed User-role member — should NOT appear.
        var confirmedEmail = $"confirmed-user-{Guid.NewGuid()}@example.com";
        await _factory.LoginWithNewAccount(confirmedEmail);
        var confirmedOrgUser = await OrganizationTestHelpers.CreateUserAsync(_factory, organization.Id, confirmedEmail,
            OrganizationUserType.User, userStatusType: OrganizationUserStatusType.Confirmed);

        await _loginHelper.LoginAsync(_ownerEmail);

        var response = await _client.GetAsync($"organizations/{organization.Id}/users/pending-auto-confirm");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ListResponseModel<OrganizationUserPendingAutoConfirmResponseModel>>();
        Assert.NotNull(body);
        var result = Assert.Single(body.Data);
        Assert.Equal(pendingOrgUser.Id, result.Id);
        Assert.NotEqual(confirmedOrgUser.Id, result.Id);

    }

    [Fact]
    public async Task GetPendingAutoConfirm_WhenUserLacksManageUsersPermission_ReturnsForbidden()
    {
        var (organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory,
            plan: PlanType.EnterpriseAnnually,
            ownerEmail: _ownerEmail,
            passwordManagerSeats: 5,
            paymentMethod: PaymentMethodType.Card);

        organization.UseAutomaticUserConfirmation = true;
        await _factory.GetService<IOrganizationRepository>().UpsertAsync(organization);

        var (regularEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory, organization.Id, OrganizationUserType.User,
            new Permissions { ManageUsers = false });
        await _loginHelper.LoginAsync(regularEmail);

        var response = await _client.GetAsync($"organizations/{organization.Id}/users/pending-auto-confirm");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

    }

    [Fact]
    public async Task BulkAutoConfirm_WhenOwnerConfirmsAcceptedUsers_ConfirmsAll()
    {
        var (organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory,
            plan: PlanType.EnterpriseAnnually,
            ownerEmail: _ownerEmail,
            passwordManagerSeats: 10,
            paymentMethod: PaymentMethodType.Card);

        organization.UseAutomaticUserConfirmation = true;
        await _factory.GetService<IOrganizationRepository>().UpsertAsync(organization);

        await _factory.GetService<IPolicyRepository>().CreateAsync(new Core.AdminConsole.Entities.Policy
        {
            OrganizationId = organization.Id,
            Type = PolicyType.AutomaticUserConfirmation,
            Enabled = true
        });

        var user1Email = $"bulk-user1-{Guid.NewGuid()}@example.com";
        await _factory.LoginWithNewAccount(user1Email);
        var orgUser1 = await OrganizationTestHelpers.CreateUserAsync(_factory, organization.Id, user1Email,
            OrganizationUserType.User, userStatusType: OrganizationUserStatusType.Accepted);

        var user2Email = $"bulk-user2-{Guid.NewGuid()}@example.com";
        await _factory.LoginWithNewAccount(user2Email);
        var orgUser2 = await OrganizationTestHelpers.CreateUserAsync(_factory, organization.Id, user2Email,
            OrganizationUserType.User, userStatusType: OrganizationUserStatusType.Accepted);

        await _loginHelper.LoginAsync(_ownerEmail);

        var requestModel = new OrganizationUserBulkConfirmRequestModel
        {
            Keys =
            [
                new OrganizationUserBulkConfirmRequestModelEntry { Id = orgUser1.Id, Key = _mockEncryptedString },
                new OrganizationUserBulkConfirmRequestModelEntry { Id = orgUser2.Id, Key = _mockEncryptedString2 }
            ],
            DefaultUserCollectionName = _mockEncryptedString
        };

        var response = await _client.PostAsJsonAsync(
            $"organizations/{organization.Id}/users/bulk-auto-confirm", requestModel);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ListResponseModel<OrganizationUserBulkResponseModel>>();
        Assert.NotNull(body);
        Assert.Equal(2, body.Data.Count());
        Assert.All(body.Data, item => Assert.True(string.IsNullOrEmpty(item.Error)));

        var orgUserRepository = _factory.GetService<IOrganizationUserRepository>();
        var confirmedUser1 = await orgUserRepository.GetByIdAsync(orgUser1.Id);
        var confirmedUser2 = await orgUserRepository.GetByIdAsync(orgUser2.Id);

        // Users are transitioned to Confirmed with the supplied key stored.
        Assert.Equal(OrganizationUserStatusType.Confirmed, confirmedUser1!.Status);
        Assert.Equal(_mockEncryptedString, confirmedUser1.Key);

        Assert.Equal(OrganizationUserStatusType.Confirmed, confirmedUser2!.Status);
        Assert.Equal(_mockEncryptedString2, confirmedUser2.Key);

    }

    [Fact]
    public async Task BulkAutoConfirm_WhenUserLacksManageUsersPermission_ReturnsForbidden()
    {
        var (organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory,
            plan: PlanType.EnterpriseAnnually,
            ownerEmail: _ownerEmail,
            passwordManagerSeats: 5,
            paymentMethod: PaymentMethodType.Card);

        organization.UseAutomaticUserConfirmation = true;
        await _factory.GetService<IOrganizationRepository>().UpsertAsync(organization);

        var (regularEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory, organization.Id, OrganizationUserType.User,
            new Permissions { ManageUsers = false });
        await _loginHelper.LoginAsync(regularEmail);

        var requestModel = new OrganizationUserBulkConfirmRequestModel
        {
            Keys = [new OrganizationUserBulkConfirmRequestModelEntry { Id = Guid.NewGuid(), Key = _mockEncryptedString }],
            DefaultUserCollectionName = _mockEncryptedString
        };

        var response = await _client.PostAsJsonAsync(
            $"organizations/{organization.Id}/users/bulk-auto-confirm", requestModel);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

    }

    [Fact]
    public async Task BulkAutoConfirm_WhenSomeUserIdsDoNotExist_ReturnsErrorsForMissing()
    {
        var (organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory,
            plan: PlanType.EnterpriseAnnually,
            ownerEmail: _ownerEmail,
            passwordManagerSeats: 5,
            paymentMethod: PaymentMethodType.Card);

        organization.UseAutomaticUserConfirmation = true;
        await _factory.GetService<IOrganizationRepository>().UpsertAsync(organization);

        await _factory.GetService<IPolicyRepository>().CreateAsync(new Core.AdminConsole.Entities.Policy
        {
            OrganizationId = organization.Id,
            Type = PolicyType.AutomaticUserConfirmation,
            Enabled = true
        });

        var userEmail = $"bulk-missing-user-{Guid.NewGuid()}@example.com";
        await _factory.LoginWithNewAccount(userEmail);
        var existingOrgUser = await OrganizationTestHelpers.CreateUserAsync(_factory, organization.Id, userEmail,
            OrganizationUserType.User, userStatusType: OrganizationUserStatusType.Accepted);

        var nonExistentId = Guid.NewGuid();

        await _loginHelper.LoginAsync(_ownerEmail);

        var requestModel = new OrganizationUserBulkConfirmRequestModel
        {
            Keys =
            [
                new OrganizationUserBulkConfirmRequestModelEntry { Id = existingOrgUser.Id, Key = _mockEncryptedString },
                new OrganizationUserBulkConfirmRequestModelEntry { Id = nonExistentId, Key = _mockEncryptedString }
            ],
            DefaultUserCollectionName = _mockEncryptedString
        };

        var response = await _client.PostAsJsonAsync(
            $"organizations/{organization.Id}/users/bulk-auto-confirm", requestModel);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ListResponseModel<OrganizationUserBulkResponseModel>>();
        Assert.NotNull(body);
        Assert.Equal(2, body.Data.Count());

        // The existing user should have been confirmed with no error.
        var existingResult = body.Data.Single(d => d.Id == existingOrgUser.Id);
        Assert.True(string.IsNullOrEmpty(existingResult.Error));

        // The non-existent user should carry an error message.
        var missingResult = body.Data.Single(d => d.Id == nonExistentId);
        Assert.False(string.IsNullOrEmpty(missingResult.Error));

        var confirmedUser = await _factory.GetService<IOrganizationUserRepository>().GetByIdAsync(existingOrgUser.Id);
        Assert.Equal(OrganizationUserStatusType.Confirmed, confirmedUser!.Status);

    }

}
