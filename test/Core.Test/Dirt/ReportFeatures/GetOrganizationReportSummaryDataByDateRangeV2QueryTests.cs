using Bit.Core.Dirt.Models.Data;
using Bit.Core.Dirt.Reports.ReportFeatures;
using Bit.Core.Dirt.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Dirt.ReportFeatures;

[SutProviderCustomize]
public class GetOrganizationReportSummaryDataByDateRangeV2QueryTests
{
    [Theory]
    [BitAutoData]
    public async Task GetSummaryDataByDateRangeAsync_ValidParams_ReturnsList(
        SutProvider<GetOrganizationReportSummaryDataByDateRangeV2Query> sutProvider)
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var startDate = DateTime.UtcNow.AddDays(-30);
        var endDate = DateTime.UtcNow;

        var expectedData = new List<OrganizationReportSummaryDataResponse>
        {
            new() { SummaryData = "summary-1" },
            new() { SummaryData = "summary-2" }
        };

        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetSummaryDataByDateRangeAsync(organizationId, startDate, endDate)
            .Returns(expectedData);

        // Act
        var result = await sutProvider.Sut.GetSummaryDataByDateRangeAsync(organizationId, startDate, endDate);

        // Assert
        var resultList = result.ToList();
        Assert.Equal(2, resultList.Count);
        Assert.Equal("summary-1", resultList[0].SummaryData);
        Assert.Equal("summary-2", resultList[1].SummaryData);
    }

    [Theory]
    [BitAutoData]
    public async Task GetSummaryDataByDateRangeAsync_NoResults_ReturnsEmptyEnumerable(
        SutProvider<GetOrganizationReportSummaryDataByDateRangeV2Query> sutProvider)
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var startDate = DateTime.UtcNow.AddDays(-30);
        var endDate = DateTime.UtcNow;

        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetSummaryDataByDateRangeAsync(organizationId, startDate, endDate)
            .Returns(new List<OrganizationReportSummaryDataResponse>());

        // Act
        var result = await sutProvider.Sut.GetSummaryDataByDateRangeAsync(organizationId, startDate, endDate);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Theory]
    [BitAutoData]
    public async Task GetSummaryDataByDateRangeAsync_NullFromRepo_ReturnsEmptyEnumerable(
        SutProvider<GetOrganizationReportSummaryDataByDateRangeV2Query> sutProvider)
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var startDate = DateTime.UtcNow.AddDays(-30);
        var endDate = DateTime.UtcNow;

        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetSummaryDataByDateRangeAsync(organizationId, startDate, endDate)
            .Returns(null as IEnumerable<OrganizationReportSummaryDataResponse>);

        // Act
        var result = await sutProvider.Sut.GetSummaryDataByDateRangeAsync(organizationId, startDate, endDate);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }
}
