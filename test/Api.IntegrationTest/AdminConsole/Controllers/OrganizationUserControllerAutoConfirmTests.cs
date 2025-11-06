using System.Net;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
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

    private static (ApiApplicationFactory factory, HttpClient client, LoginHelper loginHelper) GetAutoConfirmTestFactoryAsync()
    {
        var localFactory = new ApiApplicationFactory();
        localFactory.SubstituteService<IFeatureService>(featureService =>
        {
            featureService
                .IsEnabled(FeatureFlagKeys.AutomaticConfirmUsers)
                .Returns(true);
        });
        var localClient = localFactory.CreateClient();
        var localLoginHelper = new LoginHelper(localFactory, localClient);

        return (localFactory, localClient, localLoginHelper);
    }

    [Fact]
    public async Task AutoConfirm_WhenAutoConfirmFlagIsDisabled_ThenShouldReturnNotFound()
    {
        var testKey = $"test-key-{Guid.NewGuid()}";
        var defaultCollectionName = _mockEncryptedString;

        await _loginHelper.LoginAsync(_ownerEmail);

        var (userEmail, organizationUser) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory,
            _organization.Id, OrganizationUserType.User);

        var result = await _client.PostAsJsonAsync($"organizations/{_organization.Id}/users/{organizationUser.Id}/auto-confirm",
            new OrganizationUserConfirmRequestModel
            {
                Key = testKey,
                DefaultUserCollectionName = defaultCollectionName
            });

        Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
    }

    [Fact]
    public async Task AutoConfirm_WhenUserCannotManageOtherUsers_ThenShouldReturnForbidden()
    {
        var (factory, client, loginHelper) = GetAutoConfirmTestFactoryAsync();

        var ownerEmail = $"org-user-integration-test-{Guid.NewGuid()}@bitwarden.com";
        await factory.LoginWithNewAccount(ownerEmail);

        var (organization, _) = await OrganizationTestHelpers.SignUpAsync(factory, plan: PlanType.EnterpriseAnnually2023,
            ownerEmail: ownerEmail, passwordManagerSeats: 5, paymentMethod: PaymentMethodType.Card);

        var testKey = $"test-key-{Guid.NewGuid()}";
        var defaultCollectionName = _mockEncryptedString;

        await loginHelper.LoginAsync(ownerEmail);

        var userToConfirmEmail = $"org-user-to-confirm-{Guid.NewGuid()}@bitwarden.com";
        await factory.LoginWithNewAccount(userToConfirmEmail);

        await loginHelper.LoginAsync(userToConfirmEmail);
        var organizationUser = await OrganizationTestHelpers.CreateUserAsync(
            factory,
            organization.Id,
            userToConfirmEmail,
            OrganizationUserType.User,
            false,
            new Permissions(),
            OrganizationUserStatusType.Accepted);

        var result = await client.PostAsJsonAsync($"organizations/{organization.Id}/users/{organizationUser.Id}/auto-confirm",
            new OrganizationUserConfirmRequestModel
            {
                Key = testKey,
                DefaultUserCollectionName = defaultCollectionName
            });

        Assert.Equal(HttpStatusCode.Forbidden, result.StatusCode);
    }

    [Fact]
    public async Task AutoConfirm_WhenOwnerConfirmsValidUser_ThenShouldReturnNoContent()
    {
        var (factory, client, loginHelper) = GetAutoConfirmTestFactoryAsync();

        var ownerEmail = $"org-user-integration-test-{Guid.NewGuid()}@bitwarden.com";
        await factory.LoginWithNewAccount(ownerEmail);

        var (organization, _) = await OrganizationTestHelpers.SignUpAsync(factory, plan: PlanType.EnterpriseAnnually2023,
            ownerEmail: ownerEmail, passwordManagerSeats: 5, paymentMethod: PaymentMethodType.Card);

        var testKey = $"test-key-{Guid.NewGuid()}";
        var defaultCollectionName = _mockEncryptedString;

        await loginHelper.LoginAsync(ownerEmail);

        var userToConfirmEmail = $"org-user-to-confirm-{Guid.NewGuid()}@bitwarden.com";
        await factory.LoginWithNewAccount(userToConfirmEmail);

        var organizationUser = await OrganizationTestHelpers.CreateUserAsync(
            factory,
            organization.Id,
            userToConfirmEmail,
            OrganizationUserType.User,
            false,
            new Permissions(),
            OrganizationUserStatusType.Accepted);

        var result = await client.PostAsJsonAsync($"organizations/{organization.Id}/users/{organizationUser.Id}/auto-confirm",
            new OrganizationUserConfirmRequestModel
            {
                Key = testKey,
                DefaultUserCollectionName = defaultCollectionName
            });

        Assert.Equal(HttpStatusCode.NoContent, result.StatusCode);

        var orgUserRepository = factory.GetService<IOrganizationUserRepository>();
        var confirmedUser = await orgUserRepository.GetByIdAsync(organizationUser.Id);
        Assert.NotNull(confirmedUser);
        Assert.Equal(OrganizationUserStatusType.Confirmed, confirmedUser.Status);
        Assert.Equal(testKey, confirmedUser.Key);
    }

    public async Task InitializeAsync()
    {
        _ownerEmail = $"org-user-integration-test-{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(_ownerEmail);

        (_organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory, plan: PlanType.EnterpriseAnnually2023,
            ownerEmail: _ownerEmail, passwordManagerSeats: 5, paymentMethod: PaymentMethodType.Card);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    public OrganizationUserControllerAutoConfirmTests(ApiApplicationFactory apiFactory)
    {
        _factory = apiFactory;
        _factory.SubstituteService<IFeatureService>(featureService =>
        {
            featureService
                .IsEnabled(FeatureFlagKeys.CreateDefaultLocation)
                .Returns(true);
        });
        _client = _factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
    }

    private const string _mockEncryptedString = "2.AOs41Hd8OQiCPXjyJKCiDA==|O6OHgt2U2hJGBSNGnimJmg==|iD33s8B69C8JhYYhSa4V1tArjvLr8eEaGqOV7BRo5Jk=";

    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;

    private Organization _organization = null!;
    private string _ownerEmail = null!;
}
