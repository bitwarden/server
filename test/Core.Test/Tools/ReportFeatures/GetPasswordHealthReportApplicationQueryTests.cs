using AutoFixture;
using Bit.Core.Exceptions;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.ReportFeatures;
using Bit.Core.Tools.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Tools.ReportFeatures;

[SutProviderCustomize]
public class GetPasswordHealthReportApplicationQueryTests
{
    [Theory]
    [BitAutoData]
    public async Task GetPasswordHealthReportApplicationAsync_WithValidOrganizationId_ShouldReturnPasswordHealthReportApplication(
        SutProvider<GetPasswordHealthReportApplicationQuery> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var organizationId = fixture.Create<Guid>();
        sutProvider.GetDependency<IPasswordHealthReportApplicationRepository>()
            .GetByOrganizationIdAsync(Arg.Any<Guid>())
            .Returns(fixture.CreateMany<PasswordHealthReportApplication>(2).ToList());

        // Act
        var result = await sutProvider.Sut.GetPasswordHealthReportApplicationAsync(organizationId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Count() == 2);
    }

    [Theory]
    [BitAutoData]
    public async Task GetPasswordHealthReportApplicationAsync_WithInvalidOrganizationId_ShouldFail(
        SutProvider<GetPasswordHealthReportApplicationQuery> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        sutProvider.GetDependency<IPasswordHealthReportApplicationRepository>()
            .GetByOrganizationIdAsync(Arg.Is<Guid>(x => x == Guid.Empty))
            .Returns(new List<PasswordHealthReportApplication>());

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.GetPasswordHealthReportApplicationAsync(Guid.Empty));

        // Assert
        Assert.Equal("OrganizationId is required.", exception.Message);
    }
}
