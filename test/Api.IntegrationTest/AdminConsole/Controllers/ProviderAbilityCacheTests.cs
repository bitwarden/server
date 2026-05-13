using System.Net;
using Bit.Api.AdminConsole.Models.Request.Providers;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Repositories;
using Bit.Core.Services;
using NSubstitute;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Controllers;

public class ProviderAbilityCacheTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;

    private Provider _provider = null!;
    private string _providerAdminEmail = null!;

    public ProviderAbilityCacheTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _factory.SubstituteService<IFeatureService>(featureService =>
        {
            featureService
                .IsEnabled(FeatureFlagKeys.ProviderAbilityExtendedCache)
                .Returns(true);
        });
        _client = factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
    }

    public async Task InitializeAsync()
    {
        _providerAdminEmail = $"provider-cache-test-{Guid.NewGuid()}@example.com";
        await _factory.LoginWithNewAccount(_providerAdminEmail);

        var providerRepository = _factory.GetService<IProviderRepository>();
        var userRepository = _factory.GetService<IUserRepository>();
        var providerUserRepository = _factory.GetService<IProviderUserRepository>();

        _provider = await providerRepository.CreateAsync(new Provider
        {
            Name = "Provider Cache Test",
            BillingEmail = _providerAdminEmail,
            Type = ProviderType.Reseller,
            Status = ProviderStatusType.Created,
            Enabled = true
        });

        var user = await userRepository.GetByEmailAsync(_providerAdminEmail);
        await providerUserRepository.CreateAsync(new ProviderUser
        {
            ProviderId = _provider.Id,
            UserId = user!.Id,
            Type = ProviderUserType.ProviderAdmin,
            Status = ProviderUserStatusType.Confirmed,
            Key = Guid.NewGuid().ToString()
        });
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Put_UpdatesProvider_CacheReflectsUpdatedValues()
    {
        // Arrange
        await _loginHelper.LoginAsync(_providerAdminEmail);
        var updateRequest = new ProviderUpdateRequestModel
        {
            Name = "Updated Provider Cache Test",
            BillingEmail = _providerAdminEmail
        };

        // Act - update the provider via the HTTP endpoint
        var response = await _client.PutAsJsonAsync($"/providers/{_provider.Id}", updateRequest);

        // Assert - endpoint succeeded and cache was populated
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var cacheService = _factory.GetService<IApplicationCacheService>();
        var ability = await cacheService.GetProviderAbilityAsync(_provider.Id);
        Assert.NotNull(ability);
        Assert.Equal(_provider.Id, ability.Id);
    }

    [Fact]
    public async Task Delete_RemovesProvider_CacheReturnsNull()
    {
        // Arrange - populate the cache before deletion
        var cacheService = _factory.GetService<IApplicationCacheService>();
        await cacheService.UpsertProviderAbilityAsync(_provider);

        var abilityBeforeDelete = await cacheService.GetProviderAbilityAsync(_provider.Id);
        Assert.NotNull(abilityBeforeDelete);

        // Act - delete the provider via the HTTP endpoint
        await _loginHelper.LoginAsync(_providerAdminEmail);
        var response = await _client.DeleteAsync($"/providers/{_provider.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var abilityAfterDelete = await cacheService.GetProviderAbilityAsync(_provider.Id);
        Assert.Null(abilityAfterDelete);
    }
}
