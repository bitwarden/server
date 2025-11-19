using System.Net;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Services;
using NSubstitute;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Controllers;

public class OrganizationUserControllerAutoConfirmTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private const string _mockEncryptedString = "2.AOs41Hd8OQiCPXjyJKCiDA==|O6OHgt2U2hJGBSNGnimJmg==|iD33s8B69C8JhYYhSa4V1tArjvLr8eEaGqOV7BRo5Jk=";

    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;

    private string _ownerEmail = null!;

    public OrganizationUserControllerAutoConfirmTests(ApiApplicationFactory apiFactory)
    {
        _factory = apiFactory;
        _factory.SubstituteService<IFeatureService>(featureService =>
        {
            featureService
                .IsEnabled(FeatureFlagKeys.AutomaticConfirmUsers)
                .Returns(true);
        });
        _client = _factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
    }

    public async Task InitializeAsync()
    {
        _ownerEmail = $"org-owner-{Guid.NewGuid()}@example.com";
        await _factory.LoginWithNewAccount(_ownerEmail);
    }

    [Fact]
    public async Task AutoConfirm_WhenUserCannotManageOtherUsers_ThenShouldReturnForbidden()
    {
        var (organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory, plan: PlanType.EnterpriseAnnually,
            ownerEmail: _ownerEmail, passwordManagerSeats: 5, paymentMethod: PaymentMethodType.Card);

        organization.UseAutomaticUserConfirmation = true;

        await _factory.GetService<IOrganizationRepository>()
            .UpsertAsync(organization);

        var testKey = $"test-key-{Guid.NewGuid()}";

        var userToConfirmEmail = $"org-user-to-confirm-{Guid.NewGuid()}@example.com";
        await _factory.LoginWithNewAccount(userToConfirmEmail);

        var (confirmingUserEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory, organization.Id, OrganizationUserType.User);
        await _loginHelper.LoginAsync(confirmingUserEmail);

        var organizationUser = await OrganizationTestHelpers.CreateUserAsync(
            _factory,
            organization.Id,
            userToConfirmEmail,
            OrganizationUserType.User,
            false,
            new Permissions { ManageUsers = false },
            OrganizationUserStatusType.Accepted);

        var result = await _client.PostAsJsonAsync($"organizations/{organization.Id}/users/{organizationUser.Id}/auto-confirm",
            new OrganizationUserConfirmRequestModel
            {
                Key = testKey,
                DefaultUserCollectionName = _mockEncryptedString
            });

        Assert.Equal(HttpStatusCode.Forbidden, result.StatusCode);

        await _factory.GetService<IOrganizationRepository>().DeleteAsync(organization);
    }

    [Fact]
    public async Task AutoConfirm_WhenOwnerConfirmsValidUser_ThenShouldReturnNoContent()
    {
        var (organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory, plan: PlanType.EnterpriseAnnually,
            ownerEmail: _ownerEmail, passwordManagerSeats: 5, paymentMethod: PaymentMethodType.Card);

        organization.UseAutomaticUserConfirmation = true;

        await _factory.GetService<IOrganizationRepository>()
            .UpsertAsync(organization);

        var testKey = $"test-key-{Guid.NewGuid()}";

        await _factory.GetService<IPolicyRepository>().CreateAsync(new Policy
        {
            OrganizationId = organization.Id,
            Type = PolicyType.AutomaticUserConfirmation,
            Enabled = true
        });

        await _factory.GetService<IPolicyRepository>().CreateAsync(new Policy
        {
            OrganizationId = organization.Id,
            Type = PolicyType.OrganizationDataOwnership,
            Enabled = true
        });

        var userToConfirmEmail = $"org-user-to-confirm-{Guid.NewGuid()}@example.com";
        await _factory.LoginWithNewAccount(userToConfirmEmail);

        await _loginHelper.LoginAsync(_ownerEmail);
        var organizationUser = await OrganizationTestHelpers.CreateUserAsync(
            _factory,
            organization.Id,
            userToConfirmEmail,
            OrganizationUserType.User,
            false,
            new Permissions(),
            OrganizationUserStatusType.Accepted);

        var result = await _client.PostAsJsonAsync($"organizations/{organization.Id}/users/{organizationUser.Id}/auto-confirm",
            new OrganizationUserConfirmRequestModel
            {
                Key = testKey,
                DefaultUserCollectionName = _mockEncryptedString
            });

        Assert.Equal(HttpStatusCode.NoContent, result.StatusCode);

        var orgUserRepository = _factory.GetService<IOrganizationUserRepository>();
        var confirmedUser = await orgUserRepository.GetByIdAsync(organizationUser.Id);
        Assert.NotNull(confirmedUser);
        Assert.Equal(OrganizationUserStatusType.Confirmed, confirmedUser.Status);
        Assert.Equal(testKey, confirmedUser.Key);

        var collectionRepository = _factory.GetService<ICollectionRepository>();
        var collections = await collectionRepository.GetManyByUserIdAsync(organizationUser.UserId!.Value);

        Assert.NotEmpty(collections);
        Assert.Single(collections.Where(c => c.Type == CollectionType.DefaultUserCollection));

        await _factory.GetService<IOrganizationRepository>().DeleteAsync(organization);
    }

    [Fact]
    public async Task AutoConfirm_WhenUserIsConfirmedMultipleTimes_ThenShouldSuccessAndOnlyConfirmOneUser()
    {
        var (organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory, plan: PlanType.EnterpriseAnnually,
            ownerEmail: _ownerEmail, passwordManagerSeats: 5, paymentMethod: PaymentMethodType.Card);

        organization.UseAutomaticUserConfirmation = true;

        await _factory.GetService<IOrganizationRepository>()
            .UpsertAsync(organization);

        var testKey = $"test-key-{Guid.NewGuid()}";

        var userToConfirmEmail = $"org-user-to-confirm-{Guid.NewGuid()}@example.com";
        await _factory.LoginWithNewAccount(userToConfirmEmail);

        await _factory.GetService<IPolicyRepository>().CreateAsync(new Policy
        {
            OrganizationId = organization.Id,
            Type = PolicyType.AutomaticUserConfirmation,
            Enabled = true
        });

        await _factory.GetService<IPolicyRepository>().CreateAsync(new Policy
        {
            OrganizationId = organization.Id,
            Type = PolicyType.OrganizationDataOwnership,
            Enabled = true
        });

        await _loginHelper.LoginAsync(_ownerEmail);

        var organizationUser = await OrganizationTestHelpers.CreateUserAsync(
            _factory,
            organization.Id,
            userToConfirmEmail,
            OrganizationUserType.User,
            false,
            new Permissions(),
            OrganizationUserStatusType.Accepted);

        var tenRequests = Enumerable.Range(0, 10)
            .Select(_ => _client.PostAsJsonAsync($"organizations/{organization.Id}/users/{organizationUser.Id}/auto-confirm",
                new OrganizationUserConfirmRequestModel
                {
                    Key = testKey,
                    DefaultUserCollectionName = _mockEncryptedString
                })).ToList();

        var results = await Task.WhenAll(tenRequests);

        Assert.Contains(results, r => r.StatusCode == HttpStatusCode.NoContent);

        var orgUserRepository = _factory.GetService<IOrganizationUserRepository>();
        var confirmedUser = await orgUserRepository.GetByIdAsync(organizationUser.Id);
        Assert.NotNull(confirmedUser);
        Assert.Equal(OrganizationUserStatusType.Confirmed, confirmedUser.Status);
        Assert.Equal(testKey, confirmedUser.Key);

        var collections = await _factory.GetService<ICollectionRepository>()
            .GetManyByUserIdAsync(organizationUser.UserId!.Value);
        Assert.NotEmpty(collections);
        // validates user only received one default collection
        Assert.Single(collections.Where(c => c.Type == CollectionType.DefaultUserCollection));

        await _factory.GetService<IOrganizationRepository>().DeleteAsync(organization);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }
}
