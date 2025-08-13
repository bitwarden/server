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
public class GetOrganizationReportDataQueryTests
{
    [Theory]
    [BitAutoData]
    public async Task GetOrganizationReportDataAsync_WithValidParams_ShouldReturnReportData(
        SutProvider<GetOrganizationReportDataQuery> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var organizationId = fixture.Create<Guid>();
        var reportId = fixture.Create<Guid>();
        var reportDataResponse = fixture.Build<OrganizationReportDataResponse>()
            .With(x => x.OrganizationId, organizationId)
            .With(x => x.Id, reportId)
            .Create();

        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetReportDataAsync(organizationId, reportId)
            .Returns(reportDataResponse);

        // Act
        var result = await sutProvider.Sut.GetOrganizationReportDataAsync(organizationId, reportId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(organizationId, result.OrganizationId);
        Assert.Equal(reportId, result.Id);
        await sutProvider.GetDependency<IOrganizationReportRepository>()
            .Received(1).GetReportDataAsync(organizationId, reportId);
    }

    [Theory]
    [BitAutoData]
    public async Task GetOrganizationReportDataAsync_WithEmptyOrganizationId_ShouldThrowBadRequestException(
        SutProvider<GetOrganizationReportDataQuery> sutProvider)
    {
        // Arrange
        var reportId = Guid.NewGuid();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.GetOrganizationReportDataAsync(Guid.Empty, reportId));

        Assert.Equal("OrganizationId is required.", exception.Message);
        await sutProvider.GetDependency<IOrganizationReportRepository>()
            .DidNotReceive().GetReportDataAsync(Arg.Any<Guid>(), Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData]
    public async Task GetOrganizationReportDataAsync_WithEmptyReportId_ShouldThrowBadRequestException(
        SutProvider<GetOrganizationReportDataQuery> sutProvider)
    {
        // Arrange
        var organizationId = Guid.NewGuid();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.GetOrganizationReportDataAsync(organizationId, Guid.Empty));

        Assert.Equal("ReportId is required.", exception.Message);
        await sutProvider.GetDependency<IOrganizationReportRepository>()
            .DidNotReceive().GetReportDataAsync(Arg.Any<Guid>(), Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData]
    public async Task GetOrganizationReportDataAsync_WhenDataNotFound_ShouldThrowNotFoundException(
        SutProvider<GetOrganizationReportDataQuery> sutProvider)
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var reportId = Guid.NewGuid();

        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetReportDataAsync(organizationId, reportId)
            .Returns((OrganizationReportDataResponse)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(async () =>
            await sutProvider.Sut.GetOrganizationReportDataAsync(organizationId, reportId));

        Assert.Equal("Organization report data not found.", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task GetOrganizationReportDataAsync_WhenRepositoryThrowsException_ShouldPropagateException(
        SutProvider<GetOrganizationReportDataQuery> sutProvider)
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var expectedMessage = "Database connection failed";

        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetReportDataAsync(organizationId, reportId)
            .Throws(new InvalidOperationException(expectedMessage));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await sutProvider.Sut.GetOrganizationReportDataAsync(organizationId, reportId));

        Assert.Equal(expectedMessage, exception.Message);
    }
}
