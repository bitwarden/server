using AutoFixture;
using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Reports.ReportFeatures;
using Bit.Core.Dirt.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Dirt.ReportFeatures;

[SutProviderCustomize]
public class GetOrganizationReportSummaryDataV2QueryTests
{
    [Theory]
    [BitAutoData]
    public async Task GetSummaryDataAsync_ValidParams_ReturnsResponse(
        SutProvider<GetOrganizationReportSummaryDataV2Query> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var organizationId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var expectedSummaryData = "test-summary-data";

        var report = fixture.Build<OrganizationReport>()
            .With(r => r.Id, reportId)
            .With(r => r.OrganizationId, organizationId)
            .With(r => r.SummaryData, expectedSummaryData)
            .Create();

        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetByIdAsync(reportId)
            .Returns(report);

        // Act
        var result = await sutProvider.Sut.GetSummaryDataAsync(organizationId, reportId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedSummaryData, result.SummaryData);
    }

    [Theory]
    [BitAutoData]
    public async Task GetSummaryDataAsync_ReportNotFound_ReturnsNull(
        SutProvider<GetOrganizationReportSummaryDataV2Query> sutProvider)
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var reportId = Guid.NewGuid();

        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetByIdAsync(reportId)
            .Returns(null as OrganizationReport);

        // Act
        var result = await sutProvider.Sut.GetSummaryDataAsync(organizationId, reportId);

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [BitAutoData]
    public async Task GetSummaryDataAsync_OrganizationMismatch_ReturnsNull(
        SutProvider<GetOrganizationReportSummaryDataV2Query> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var organizationId = Guid.NewGuid();
        var differentOrgId = Guid.NewGuid();
        var reportId = Guid.NewGuid();

        var report = fixture.Build<OrganizationReport>()
            .With(r => r.Id, reportId)
            .With(r => r.OrganizationId, differentOrgId)
            .Create();

        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetByIdAsync(reportId)
            .Returns(report);

        // Act
        var result = await sutProvider.Sut.GetSummaryDataAsync(organizationId, reportId);

        // Assert
        Assert.Null(result);
    }
}
