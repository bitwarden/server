using System.Net;
using Bit.Api.Auth.Models.Request.Accounts;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Api.Models.Request.Organizations;
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

        var cacheService = _factory.GetService<IApplicationCacheService>();
        var abilityBefore = await cacheService.GetOrganizationAbilityAsync(_organization.Id);
        Assert.NotNull(abilityBefore);
        Assert.False(abilityBefore.LimitCollectionCreation);

        var updateRequest = new OrganizationCollectionManagementUpdateRequestModel
        {
            LimitCollectionCreation = true,
            LimitCollectionDeletion = false,
            LimitItemDeletion = false,
            AllowAdminAccessToAllCollectionItems = true
        };

        // Act - update collection management settings via the HTTP endpoint
        var response = await _client.PutAsJsonAsync(
            $"/organizations/{_organization.Id}/collection-management", updateRequest);

        // Assert - endpoint succeeded and cache reflects the updated value
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var abilityAfter = await cacheService.GetOrganizationAbilityAsync(_organization.Id);
        Assert.NotNull(abilityAfter);
        Assert.True(abilityAfter.LimitCollectionCreation);
    }

    [Fact]
    public async Task Delete_RemovesOrganization_CacheReturnsNull()
    {
        // Arrange - setup in InitializeAsync()
        await _loginHelper.LoginAsync(_ownerEmail);

        // Verify cache is populated before delete
        var cacheService = _factory.GetService<IApplicationCacheService>();
        var abilityBeforeDelete = await cacheService.GetOrganizationAbilityAsync(_organization.Id);
        Assert.NotNull(abilityBeforeDelete);

        // Act - delete the organization via the HTTP endpoint
        await _loginHelper.LoginAsync(_ownerEmail);
        var deleteRequest = new SecretVerificationRequestModel
        {
            MasterPasswordHash = "master_password_hash"
        };
        var response = await _client.PostAsJsonAsync(
            $"/organizations/{_organization.Id}/delete", deleteRequest);

        // Assert - endpoint succeeded and cache was cleared
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var abilityAfterDelete = await cacheService.GetOrganizationAbilityAsync(_organization.Id);
        Assert.Null(abilityAfterDelete);
    }
}
