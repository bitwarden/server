using AutoFixture;
using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Reports.ReportFeatures;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Exceptions;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Dirt.ReportFeatures;

[SutProviderCustomize]
public class GetOrganizationReportQueryTests
{
    [Theory]
    [BitAutoData]
    public async Task GetOrganizationReportAsync_WithValidOrganizationId_ShouldReturnOrganizationReport(
        SutProvider<GetOrganizationReportQuery> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var organizationId = fixture.Create<Guid>();
        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetByOrganizationIdAsync(Arg.Any<Guid>())
            .Returns(fixture.CreateMany<OrganizationReport>(2).ToList());

        // Act
        var result = await sutProvider.Sut.GetOrganizationReportAsync(organizationId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Count() == 2);
    }

    [Theory]
    [BitAutoData]
    public async Task GetOrganizationReportAsync_WithInvalidOrganizationId_ShouldFail(
        SutProvider<GetOrganizationReportQuery> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetByOrganizationIdAsync(Arg.Is<Guid>(x => x == Guid.Empty))
            .Returns(new List<OrganizationReport>());

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.GetOrganizationReportAsync(Guid.Empty));

        // Assert
        Assert.Equal("OrganizationId is required.", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task GetLatestOrganizationReportAsync_WithValidOrganizationId_ShouldReturnOrganizationReport(
        SutProvider<GetOrganizationReportQuery> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var organizationId = fixture.Create<Guid>();
        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetLatestByOrganizationIdAsync(Arg.Any<Guid>())
            .Returns(fixture.Create<OrganizationReport>());

        // Act
        var result = await sutProvider.Sut.GetLatestOrganizationReportAsync(organizationId);

        // Assert
        Assert.NotNull(result);
    }

    [Theory]
    [BitAutoData]
    public async Task GetLatestOrganizationReportAsync_WithInvalidOrganizationId_ShouldFail(
    SutProvider<GetOrganizationReportQuery> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetLatestByOrganizationIdAsync(Arg.Is<Guid>(x => x == Guid.Empty))
            .Returns(default(OrganizationReport));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.GetOrganizationReportAsync(Guid.Empty));

        // Assert
        Assert.Equal("OrganizationId is required.", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task GetOrganizationReportAsync_WithNoReports_ShouldReturnEmptyList(
        SutProvider<GetOrganizationReportQuery> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var organizationId = fixture.Create<Guid>();
        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetByOrganizationIdAsync(Arg.Any<Guid>())
            .Returns(new List<OrganizationReport>());

        // Act
        var result = await sutProvider.Sut.GetOrganizationReportAsync(organizationId);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }
    [Theory]
    [BitAutoData]
    public async Task GetLatestOrganizationReportAsync_WithNoReports_ShouldReturnNull(
        SutProvider<GetOrganizationReportQuery> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var organizationId = fixture.Create<Guid>();
        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetLatestByOrganizationIdAsync(Arg.Any<Guid>())
            .Returns(default(OrganizationReport));

        // Act
        var result = await sutProvider.Sut.GetLatestOrganizationReportAsync(organizationId);

        // Assert
        Assert.Null(result);
    }
    [Theory]
    [BitAutoData]
    public async Task GetOrganizationReportAsync_WithNullOrganizationId_ShouldThrowException(
        SutProvider<GetOrganizationReportQuery> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var organizationId = default(Guid);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.GetOrganizationReportAsync(organizationId));

        // Assert
        Assert.Equal("OrganizationId is required.", exception.Message);
    }
    [Theory]
    [BitAutoData]
    public async Task GetLatestOrganizationReportAsync_WithNullOrganizationId_ShouldThrowException(
        SutProvider<GetOrganizationReportQuery> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var organizationId = default(Guid);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.GetLatestOrganizationReportAsync(organizationId));

        // Assert
        Assert.Equal("OrganizationId is required.", exception.Message);
    }
    [Theory]
    [BitAutoData]
    public async Task GetOrganizationReportAsync_WithInvalidOrganizationId_ShouldThrowException(
        SutProvider<GetOrganizationReportQuery> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var organizationId = Guid.Empty;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.GetOrganizationReportAsync(organizationId));

        // Assert
        Assert.Equal("OrganizationId is required.", exception.Message);
    }
    [Theory]
    [BitAutoData]
    public async Task GetLatestOrganizationReportAsync_WithInvalidOrganizationId_ShouldThrowException(
        SutProvider<GetOrganizationReportQuery> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var organizationId = Guid.Empty;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.GetLatestOrganizationReportAsync(organizationId));

        // Assert
        Assert.Equal("OrganizationId is required.", exception.Message);
    }
}
