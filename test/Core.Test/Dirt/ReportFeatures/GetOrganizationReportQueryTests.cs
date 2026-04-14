using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Reports.ReportFeatures;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Exceptions;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Core.Test.Dirt.ReportFeatures;

[SutProviderCustomize]
public class GetOrganizationReportQueryTests
{
    [Theory, BitAutoData]
    public async Task GetLatestOrganizationReportAsync_FilterByValidated_PassesToRepository(
        SutProvider<GetOrganizationReportQuery> sutProvider,
        Guid orgId,
        OrganizationReport expectedReport)
    {
        // Arrange
        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetLatestByOrganizationIdAsync(orgId, true)
            .Returns(expectedReport);

        // Act
        var result = await sutProvider.Sut.GetLatestOrganizationReportAsync(orgId, true);

        // Assert
        Assert.Equal(expectedReport, result);
        await sutProvider.GetDependency<IOrganizationReportRepository>()
            .Received(1)
            .GetLatestByOrganizationIdAsync(orgId, true);
    }

    [Theory, BitAutoData]
    public async Task GetLatestOrganizationReportAsync_NoFilter_PassesFalseToRepository(
        SutProvider<GetOrganizationReportQuery> sutProvider,
        Guid orgId,
        OrganizationReport expectedReport)
    {
        // Arrange
        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetLatestByOrganizationIdAsync(orgId, false)
            .Returns(expectedReport);

        // Act
        var result = await sutProvider.Sut.GetLatestOrganizationReportAsync(orgId, false);

        // Assert
        Assert.Equal(expectedReport, result);
        await sutProvider.GetDependency<IOrganizationReportRepository>()
            .Received(1)
            .GetLatestByOrganizationIdAsync(orgId, false);
    }

    [Theory, BitAutoData]
    public async Task GetLatestOrganizationReportAsync_NullResult_ThrowsNotFoundException(
        SutProvider<GetOrganizationReportQuery> sutProvider,
        Guid orgId)
    {
        // Arrange
        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetLatestByOrganizationIdAsync(orgId, true)
            .Returns((OrganizationReport)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetLatestOrganizationReportAsync(orgId, true));
    }
}
