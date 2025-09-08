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
public class GetOrganizationReportSummaryDataQueryTests
{
    [Theory]
    [BitAutoData]
    public async Task GetOrganizationReportSummaryDataAsync_WithValidParams_ShouldReturnSummaryData(
        SutProvider<GetOrganizationReportSummaryDataQuery> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var organizationId = fixture.Create<Guid>();
        var reportId = fixture.Create<Guid>();
        var summaryDataResponse = fixture.Build<OrganizationReportSummaryDataResponse>()
            .Create();

        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetSummaryDataAsync(organizationId, reportId)
            .Returns(summaryDataResponse);

        // Act
        var result = await sutProvider.Sut.GetOrganizationReportSummaryDataAsync(organizationId, reportId);

        // Assert
        Assert.NotNull(result);
        await sutProvider.GetDependency<IOrganizationReportRepository>()
            .Received(1).GetSummaryDataAsync(organizationId, reportId);
    }

    [Theory]
    [BitAutoData]
    public async Task GetOrganizationReportSummaryDataAsync_WithEmptyOrganizationId_ShouldThrowBadRequestException(
        SutProvider<GetOrganizationReportSummaryDataQuery> sutProvider)
    {
        // Arrange
        var reportId = Guid.NewGuid();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.GetOrganizationReportSummaryDataAsync(Guid.Empty, reportId));

        Assert.Equal("OrganizationId is required.", exception.Message);
        await sutProvider.GetDependency<IOrganizationReportRepository>()
            .DidNotReceive().GetSummaryDataAsync(Arg.Any<Guid>(), Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData]
    public async Task GetOrganizationReportSummaryDataAsync_WithEmptyReportId_ShouldThrowBadRequestException(
        SutProvider<GetOrganizationReportSummaryDataQuery> sutProvider)
    {
        // Arrange
        var organizationId = Guid.NewGuid();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.GetOrganizationReportSummaryDataAsync(organizationId, Guid.Empty));

        Assert.Equal("ReportId is required.", exception.Message);
        await sutProvider.GetDependency<IOrganizationReportRepository>()
            .DidNotReceive().GetSummaryDataAsync(Arg.Any<Guid>(), Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData]
    public async Task GetOrganizationReportSummaryDataAsync_WhenDataNotFound_ShouldThrowNotFoundException(
        SutProvider<GetOrganizationReportSummaryDataQuery> sutProvider)
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var reportId = Guid.NewGuid();

        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetSummaryDataAsync(organizationId, reportId)
            .Returns((OrganizationReportSummaryDataResponse)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(async () =>
            await sutProvider.Sut.GetOrganizationReportSummaryDataAsync(organizationId, reportId));

        Assert.Equal("Organization report summary data not found.", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task GetOrganizationReportSummaryDataAsync_WhenRepositoryThrowsException_ShouldPropagateException(
        SutProvider<GetOrganizationReportSummaryDataQuery> sutProvider)
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var expectedMessage = "Database connection failed";

        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetSummaryDataAsync(organizationId, reportId)
            .Throws(new InvalidOperationException(expectedMessage));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await sutProvider.Sut.GetOrganizationReportSummaryDataAsync(organizationId, reportId));

        Assert.Equal(expectedMessage, exception.Message);
    }
}
