using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Core.Platform.Installations.Tests;

[SutProviderCustomize]
public class UpdateInstallationCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task UpdateLastActivityDateAsync_ShouldUpdateLastActivityDate(
        Installation installation
    )
    {
        // Arrange
        var sutProvider = new SutProvider<UpdateInstallationCommand>()
            .WithFakeTimeProvider()
            .Create();

        var someDate = new DateTime(2014, 11, 3, 18, 27, 0, DateTimeKind.Utc);
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(someDate);

        sutProvider
            .GetDependency<IGetInstallationQuery>()
            .GetByIdAsync(installation.Id)
            .Returns(installation);

        // Act
        await sutProvider.Sut.UpdateLastActivityDateAsync(installation.Id);

        // Assert
        await sutProvider
            .GetDependency<IInstallationRepository>()
            .Received(1)
            .UpsertAsync(Arg.Is<Installation>(inst => inst.LastActivityDate == someDate));
    }
}
