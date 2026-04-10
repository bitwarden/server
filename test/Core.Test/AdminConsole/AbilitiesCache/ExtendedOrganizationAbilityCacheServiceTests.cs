using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace Bit.Core.Test.AdminConsole.AbilitiesCache;

[SutProviderCustomize]
public class ExtendedOrganizationAbilityCacheServiceTests
{
    [Theory, BitAutoData]
    public async Task GetOrganizationAbilityAsync_CallsGetOrSetAsyncWithCorrectKey(
        SutProvider<ExtendedOrganizationAbilityCacheService> sutProvider,
        Guid orgId,
        OrganizationAbility expectedAbility)
    {
        // Arrange
        var expectedKey = OrganizationAbilityCacheConstants.BuildCacheKeyForOrganizationAbility(orgId);
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetAbilityAsync(orgId)
            .Returns(expectedAbility);

        sutProvider.GetDependency<IFusionCache>()
            .GetOrSetAsync(
                key: Arg.Is(expectedKey),
                factory: Arg.Any<Func<object, CancellationToken, Task<OrganizationAbility?>>>(),
                options: Arg.Any<FusionCacheEntryOptions>(),
                tags: Arg.Any<IEnumerable<string>>())
            .Returns(callInfo =>
            {
                var factory = callInfo
                    .ArgAt<Func<FusionCacheFactoryExecutionContext<OrganizationAbility?>, CancellationToken,
                        Task<OrganizationAbility?>>>(1);
                return new ValueTask<OrganizationAbility?>(factory.Invoke(null!, CancellationToken.None));
            });

        // Act
        var result = await sutProvider.Sut.GetOrganizationAbilityAsync(orgId);

        // Assert
        Assert.Equal(expectedAbility, result);
        await sutProvider.GetDependency<IOrganizationRepository>()
            .Received(1)
            .GetAbilityAsync(orgId);
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationAbilityAsync_WhenOrgDoesNotExist_ReturnsNull(
        SutProvider<ExtendedOrganizationAbilityCacheService> sutProvider,
        Guid orgId)
    {
        // Arrange
        var expectedKey = OrganizationAbilityCacheConstants.BuildCacheKeyForOrganizationAbility(orgId);
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetAbilityAsync(orgId)
            .Returns((OrganizationAbility?)null);

        sutProvider.GetDependency<IFusionCache>()
            .GetOrSetAsync(
                key: Arg.Is(expectedKey),
                factory: Arg.Any<Func<object, CancellationToken, Task<OrganizationAbility?>>>(),
                options: Arg.Any<FusionCacheEntryOptions>(),
                tags: Arg.Any<IEnumerable<string>>())
            .Returns(callInfo =>
            {
                var factory = callInfo
                    .ArgAt<Func<FusionCacheFactoryExecutionContext<OrganizationAbility?>, CancellationToken,
                        Task<OrganizationAbility?>>>(1);
                return new ValueTask<OrganizationAbility?>(factory.Invoke(null!, CancellationToken.None));
            });

        // Act
        var result = await sutProvider.Sut.GetOrganizationAbilityAsync(orgId);

        // Assert
        Assert.Null(result);
    }

    [Theory, BitAutoData]
    public async Task UpsertOrganizationAbilityAsync_CallsSetAsyncWithCorrectKey(
        SutProvider<ExtendedOrganizationAbilityCacheService> sutProvider,
        Organization organization)
    {
        // Arrange
        var expectedKey = OrganizationAbilityCacheConstants.BuildCacheKeyForOrganizationAbility(organization.Id);

        // Act
        await sutProvider.Sut.UpsertOrganizationAbilityAsync(organization);

        // Assert
        await sutProvider.GetDependency<IFusionCache>()
            .Received(1)
            .SetAsync(
                Arg.Is(expectedKey),
                Arg.Is<OrganizationAbility?>(a => a != null && a.Id == organization.Id),
                Arg.Any<FusionCacheEntryOptions>(),
                Arg.Any<IEnumerable<string>>(),
                Arg.Any<CancellationToken>());
    }

    [Theory, BitAutoData]
    public async Task DeleteOrganizationAbilityAsync_CallsRemoveAsyncWithCorrectKey(
        SutProvider<ExtendedOrganizationAbilityCacheService> sutProvider,
        Guid organizationId)
    {
        // Arrange
        var expectedKey = OrganizationAbilityCacheConstants.BuildCacheKeyForOrganizationAbility(organizationId);

        // Act
        await sutProvider.Sut.DeleteOrganizationAbilityAsync(organizationId);

        // Assert
        await sutProvider.GetDependency<IFusionCache>()
            .Received(1)
            .RemoveAsync(expectedKey);
    }
}
