using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.AdminConsole.Models.Data.Provider;
using Bit.Core.Services.Implementations;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services.Implementations;

[SutProviderCustomize]
public class FeatureRoutedCacheServiceTests
{

    [Theory, BitAutoData]
    public async Task GetProviderAbilitiesAsync_ReturnsFromInMemoryService(
        SutProvider<FeatureRoutedCacheService> sutProvider,
        IDictionary<Guid, ProviderAbility> expectedResult)
    {
        // Arrange
        sutProvider.GetDependency<IVCurrentInMemoryApplicationCacheService>()
            .GetProviderAbilitiesAsync()
            .Returns(expectedResult);

        // Act
        var result = await sutProvider.Sut.GetProviderAbilitiesAsync();

        // Assert
        Assert.Equal(expectedResult, result);
        await sutProvider.GetDependency<IVCurrentInMemoryApplicationCacheService>()
            .Received(1)
            .GetProviderAbilitiesAsync();
    }
}
