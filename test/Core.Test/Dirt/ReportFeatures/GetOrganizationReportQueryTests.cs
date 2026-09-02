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
    [Theory, BitAutoData]
    public async Task ReadLatestOrganizationReportAsync_ReturnsRepoResult(
        SutProvider<GetOrganizationReportQuery> sutProvider,
        Guid orgId,
        OrganizationReport expectedReport)
    {
        sutProvider.GetDependency<IOrganizationReportRepository>()
            .ReadLatestByOrganizationIdAsync(orgId)
            .Returns(expectedReport);

        var result = await sutProvider.Sut.ReadLatestOrganizationReportAsync(orgId);

        Assert.Equal(expectedReport, result);
    }

    [Theory, BitAutoData]
    public async Task ReadLatestOrganizationReportAsync_NullResult_ThrowsNotFoundException(
        SutProvider<GetOrganizationReportQuery> sutProvider,
        Guid orgId)
    {
        sutProvider.GetDependency<IOrganizationReportRepository>()
            .ReadLatestByOrganizationIdAsync(orgId)
            .Returns((OrganizationReport)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.ReadLatestOrganizationReportAsync(orgId));
    }

    [Theory, BitAutoData]
    public async Task GetLatestOrganizationReportAsync_ReturnsRepoResult(
        SutProvider<GetOrganizationReportQuery> sutProvider,
        Guid orgId,
        OrganizationReport expectedReport)
    {
        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetLatestByOrganizationIdAsync(orgId)
            .Returns(expectedReport);

        var result = await sutProvider.Sut.GetLatestOrganizationReportAsync(orgId);

        Assert.Equal(expectedReport, result);
    }

    [Theory, BitAutoData]
    public async Task GetLatestOrganizationReportAsync_NullResult_ThrowsNotFoundException(
        SutProvider<GetOrganizationReportQuery> sutProvider,
        Guid orgId)
    {
        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetLatestByOrganizationIdAsync(orgId)
            .Returns((OrganizationReport)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetLatestOrganizationReportAsync(orgId));
    }
}
