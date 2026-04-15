using System.Net;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.Auth.Models.Request.Accounts;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Bit.Core.Services;
using NSubstitute;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Controllers;

public class OrganizationAbilityCacheTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;

    private Organization _organization = null!;
    private string _ownerEmail = null!;

    public OrganizationAbilityCacheTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _factory.SubstituteService<IFeatureService>(featureService =>
        {
            featureService
                .IsEnabled(FeatureFlagKeys.OrgAbilityExtendedCache)
                .Returns(true);
        });
        _client = factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
    }

    public async Task InitializeAsync()
    {
        _ownerEmail = $"cache-test-{Guid.NewGuid()}@example.com";
        await _factory.LoginWithNewAccount(_ownerEmail);

        var result = await OrganizationTestHelpers.SignUpAsync(
            _factory,
            plan: PlanType.EnterpriseAnnually,
            ownerEmail: _ownerEmail,
            passwordManagerSeats: 5,
            paymentMethod: PaymentMethodType.Card);
        _organization = result.Item1;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task SignUp_PopulatesCache_GetOrganizationAbilityReturnsAbility()
    {
        // Arrange - organization already created in InitializeAsync via SignUpAsync,
        // which calls UpsertOrganizationAbilityAsync

        // Act - read the cached ability directly
        var cacheService = _factory.GetService<IApplicationCacheService>();
        var ability = await cacheService.GetOrganizationAbilityAsync(_organization.Id);

        // Assert - cache was populated by the sign-up flow
        Assert.NotNull(ability);
        Assert.Equal(_organization.Id, ability.Id);
        Assert.True(ability.Enabled);
    }

    [Fact]
    public async Task Put_UpdatesOrganization_CacheReflectsUpdatedValues()
    {
        // Arrange - setup in InitializeAsync()
        await _loginHelper.LoginAsync(_ownerEmail);
        var updateRequest = new OrganizationUpdateRequestModel
        {
            Name = "Updated Cache Test Org",
            BillingEmail = "updated-cache@example.com"
        };

        // Act - update the organization via the HTTP endpoint
        var response = await _client.PutAsJsonAsync($"/organizations/{_organization.Id}", updateRequest);

        // Assert - endpoint succeeded and cache was updated
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var cacheService = _factory.GetService<IApplicationCacheService>();
        var ability = await cacheService.GetOrganizationAbilityAsync(_organization.Id);
        Assert.NotNull(ability);
        Assert.Equal(_organization.Id, ability.Id);
    }

    [Fact]
    public async Task Delete_RemovesOrganization_CacheReturnsNull()
    {
        // Arrange - create a separate org for deletion so we don't affect other tests
        var deleteOwnerEmail = $"delete-test-{Guid.NewGuid()}@example.com";
        await _factory.LoginWithNewAccount(deleteOwnerEmail);

        var signUpResult = await OrganizationTestHelpers.SignUpAsync(
            _factory,
            plan: PlanType.EnterpriseAnnually,
            ownerEmail: deleteOwnerEmail,
            passwordManagerSeats: 5,
            paymentMethod: PaymentMethodType.Card);
        var orgToDelete = signUpResult.Item1;

        // Verify cache is populated before delete
        var cacheService = _factory.GetService<IApplicationCacheService>();
        var abilityBeforeDelete = await cacheService.GetOrganizationAbilityAsync(orgToDelete.Id);
        Assert.NotNull(abilityBeforeDelete);

        // Act - delete the organization via the HTTP endpoint
        await _loginHelper.LoginAsync(deleteOwnerEmail);
        var deleteRequest = new SecretVerificationRequestModel
        {
            MasterPasswordHash = "master_password_hash"
        };
        var response = await _client.PostAsJsonAsync(
            $"/organizations/{orgToDelete.Id}/delete", deleteRequest);

        // Assert - endpoint succeeded and cache was cleared
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var abilityAfterDelete = await cacheService.GetOrganizationAbilityAsync(orgToDelete.Id);
        Assert.Null(abilityAfterDelete);
    }
}
