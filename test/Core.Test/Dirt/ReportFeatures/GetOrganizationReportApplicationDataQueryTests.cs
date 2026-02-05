using AutoFixture;
using Bit.Core.Dirt.Models.Data;
using Bit.Core.Dirt.Reports.ReportFeatures;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Exceptions;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Bit.Core.Test.Dirt.ReportFeatures;

[SutProviderCustomize]
public class GetOrganizationReportApplicationDataQueryTests
{
    [Theory]
    [BitAutoData]
    public async Task GetOrganizationReportApplicationDataAsync_WithValidParams_ShouldReturnApplicationData(
        SutProvider<GetOrganizationReportApplicationDataQuery> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var organizationId = fixture.Create<Guid>();
        var reportId = fixture.Create<Guid>();
        var applicationDataResponse = fixture.Build<OrganizationReportApplicationDataResponse>()
            .Create();

        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetApplicationDataAsync(reportId)
            .Returns(applicationDataResponse);

        // Act
        var result = await sutProvider.Sut.GetOrganizationReportApplicationDataAsync(organizationId, reportId);

        // Assert
        Assert.NotNull(result);
        await sutProvider.GetDependency<IOrganizationReportRepository>()
            .Received(1).GetApplicationDataAsync(reportId);
    }

    [Theory]
    [BitAutoData]
    public async Task GetOrganizationReportApplicationDataAsync_WithEmptyOrganizationId_ShouldThrowBadRequestException(
        SutProvider<GetOrganizationReportApplicationDataQuery> sutProvider)
    {
        // Arrange
        var reportId = Guid.NewGuid();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.GetOrganizationReportApplicationDataAsync(Guid.Empty, reportId));

        Assert.Equal("OrganizationId is required.", exception.Message);
        await sutProvider.GetDependency<IOrganizationReportRepository>()
            .DidNotReceive().GetApplicationDataAsync(Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData]
    public async Task GetOrganizationReportApplicationDataAsync_WithEmptyReportId_ShouldThrowBadRequestException(
        SutProvider<GetOrganizationReportApplicationDataQuery> sutProvider)
    {
        // Arrange
        var organizationId = Guid.NewGuid();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.GetOrganizationReportApplicationDataAsync(organizationId, Guid.Empty));

        Assert.Equal("ReportId is required.", exception.Message);
        await sutProvider.GetDependency<IOrganizationReportRepository>()
            .DidNotReceive().GetApplicationDataAsync(Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData]
    public async Task GetOrganizationReportApplicationDataAsync_WhenDataNotFound_ShouldThrowNotFoundException(
        SutProvider<GetOrganizationReportApplicationDataQuery> sutProvider)
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var reportId = Guid.NewGuid();

        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetApplicationDataAsync(reportId)
            .Returns((OrganizationReportApplicationDataResponse)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(async () =>
            await sutProvider.Sut.GetOrganizationReportApplicationDataAsync(organizationId, reportId));

        Assert.Equal("Organization report application data not found.", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task GetOrganizationReportApplicationDataAsync_WhenRepositoryThrowsException_ShouldPropagateException(
        SutProvider<GetOrganizationReportApplicationDataQuery> sutProvider)
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var expectedMessage = "Database connection failed";

        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetApplicationDataAsync(reportId)
            .Throws(new InvalidOperationException(expectedMessage));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await sutProvider.Sut.GetOrganizationReportApplicationDataAsync(organizationId, reportId));

        Assert.Equal(expectedMessage, exception.Message);
    }
}
