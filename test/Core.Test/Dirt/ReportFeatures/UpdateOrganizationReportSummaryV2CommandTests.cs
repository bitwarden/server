using AutoFixture;
using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Reports.Models.Data;
using Bit.Core.Dirt.Reports.ReportFeatures;
using Bit.Core.Dirt.Reports.ReportFeatures.Requests;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Exceptions;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Dirt.ReportFeatures;

[SutProviderCustomize]
public class UpdateOrganizationReportSummaryV2CommandTests
{
    [Theory]
    [BitAutoData]
    public async Task UpdateSummaryAsync_ValidRequest_ReturnsUpdatedReport(
        SutProvider<UpdateOrganizationReportSummaryV2Command> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var organizationId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var summaryData = "test-summary-data";

        var existingReport = fixture.Build<OrganizationReport>()
            .With(r => r.Id, reportId)
            .With(r => r.OrganizationId, organizationId)
            .Create();

        var updatedReport = fixture.Build<OrganizationReport>()
            .With(r => r.Id, reportId)
            .With(r => r.OrganizationId, organizationId)
            .With(r => r.SummaryData, summaryData)
            .Create();

        var request = new UpdateOrganizationReportSummaryRequest
        {
            ReportId = reportId,
            OrganizationId = organizationId,
            SummaryData = summaryData,
            ReportMetrics = new OrganizationReportMetrics
            {
                ApplicationCount = 10,
                ApplicationAtRiskCount = 2
            }
        };

        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetByIdAsync(reportId)
            .Returns(existingReport);

        sutProvider.GetDependency<IOrganizationReportRepository>()
            .UpdateSummaryDataAsync(organizationId, reportId, summaryData)
            .Returns(updatedReport);

        // Act
        var result = await sutProvider.Sut.UpdateSummaryAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(summaryData, result.SummaryData);

        await sutProvider.GetDependency<IOrganizationReportRepository>()
            .Received(1)
            .UpdateMetricsAsync(reportId, Arg.Any<OrganizationReportMetricsData>());

        await sutProvider.GetDependency<IOrganizationReportRepository>()
            .Received(1)
            .UpdateSummaryDataAsync(organizationId, reportId, summaryData);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateSummaryAsync_ReportNotFound_ThrowsNotFoundException(
        SutProvider<UpdateOrganizationReportSummaryV2Command> sutProvider)
    {
        // Arrange
        var request = new UpdateOrganizationReportSummaryRequest
        {
            ReportId = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            SummaryData = "test-data"
        };

        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetByIdAsync(request.ReportId)
            .Returns(null as OrganizationReport);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(
            async () => await sutProvider.Sut.UpdateSummaryAsync(request));
        Assert.Equal("Organization report not found", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateSummaryAsync_OrganizationMismatch_ThrowsNotFoundException(
        SutProvider<UpdateOrganizationReportSummaryV2Command> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var reportId = Guid.NewGuid();
        var requestOrgId = Guid.NewGuid();
        var differentOrgId = Guid.NewGuid();

        var existingReport = fixture.Build<OrganizationReport>()
            .With(r => r.Id, reportId)
            .With(r => r.OrganizationId, differentOrgId)
            .Create();

        var request = new UpdateOrganizationReportSummaryRequest
        {
            ReportId = reportId,
            OrganizationId = requestOrgId,
            SummaryData = "test-data"
        };

        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetByIdAsync(reportId)
            .Returns(existingReport);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(
            async () => await sutProvider.Sut.UpdateSummaryAsync(request));
        Assert.Equal("Organization report not found", exception.Message);

        await sutProvider.GetDependency<IOrganizationReportRepository>()
            .DidNotReceive()
            .UpdateSummaryDataAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>());
    }
}
