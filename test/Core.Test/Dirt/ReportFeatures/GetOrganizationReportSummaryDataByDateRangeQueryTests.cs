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
using ZiggyCreatures.Caching.Fusion;

namespace Bit.Core.Test.Dirt.ReportFeatures;

[SutProviderCustomize]
public class GetOrganizationReportSummaryDataByDateRangeQueryTests
{
    [Theory]
    [BitAutoData]
    public async Task GetOrganizationReportSummaryDataByDateRangeAsync_WithValidParams_ShouldReturnSummaryData(
        SutProvider<GetOrganizationReportSummaryDataByDateRangeQuery> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var organizationId = fixture.Create<Guid>();
        var reportId = fixture.Create<Guid>();
        var startDate = DateTime.UtcNow.AddDays(-30);
        var endDate = DateTime.UtcNow;
        var summaryDataList = fixture.Build<OrganizationReportSummaryDataResponse>()
            .CreateMany(3);

        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetSummaryDataByDateRangeAsync(organizationId, startDate, endDate)
            .Returns(summaryDataList);

        sutProvider
            .GetDependency<IFusionCache>()
            .GetOrSetAsync(
                key: Arg.Any<string?>(),
                factory: Arg.Any<Func<object, CancellationToken, Task<IEnumerable<OrganizationReportSummaryDataResponse>>>>(),
                options: Arg.Any<FusionCacheEntryOptions>(),
                tags: Arg.Any<IEnumerable<string>>())
            .Returns(callInfo =>
            {
                var factory = callInfo.ArgAt<Func<FusionCacheFactoryExecutionContext<IEnumerable<OrganizationReportSummaryDataResponse>>, CancellationToken, Task<IEnumerable<OrganizationReportSummaryDataResponse>>>>(1);
                return new ValueTask<IEnumerable<OrganizationReportSummaryDataResponse>>(factory.Invoke(null, CancellationToken.None));
            });


        // Act
        var result = await sutProvider.Sut.GetOrganizationReportSummaryDataByDateRangeAsync(organizationId, startDate, endDate);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count());
        await sutProvider.GetDependency<IOrganizationReportRepository>()
            .Received(1).GetSummaryDataByDateRangeAsync(organizationId, startDate, endDate);
    }

    [Theory]
    [BitAutoData]
    public async Task GetOrganizationReportSummaryDataByDateRangeAsync_ShouldReturnTopSixResults(
        SutProvider<GetOrganizationReportSummaryDataByDateRangeQuery> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var organizationId = fixture.Create<Guid>();
        var reportId = fixture.Create<Guid>();
        var startDate = DateTime.UtcNow.AddDays(-30);
        var endDate = DateTime.UtcNow;
        var summaryDataList = fixture.Build<OrganizationReportSummaryDataResponse>()
            .CreateMany(12)
            .ToList();
        summaryDataList[0].RevisionDate = DateTime.UtcNow; // most recent
        summaryDataList[1].RevisionDate = DateTime.UtcNow.AddDays(-1);
        summaryDataList[2].RevisionDate = DateTime.UtcNow.AddDays(-2);
        summaryDataList[3].RevisionDate = DateTime.UtcNow.AddDays(-3);
        summaryDataList[4].RevisionDate = DateTime.UtcNow.AddDays(-4);
        summaryDataList[5].RevisionDate = DateTime.UtcNow.AddDays(-5);
        summaryDataList[6].RevisionDate = DateTime.UtcNow.AddDays(-6);
        summaryDataList[7].RevisionDate = DateTime.UtcNow.AddDays(-7);
        summaryDataList[8].RevisionDate = DateTime.UtcNow.AddDays(-8);
        summaryDataList[9].RevisionDate = DateTime.UtcNow.AddDays(-9);
        summaryDataList[10].RevisionDate = DateTime.UtcNow.AddDays(-10);
        summaryDataList[11].RevisionDate = DateTime.UtcNow.AddDays(-11);

        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetSummaryDataByDateRangeAsync(Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(summaryDataList);

        sutProvider
            .GetDependency<IFusionCache>()
            .GetOrSetAsync(
                key: Arg.Any<string?>(),
                factory: Arg.Any<Func<object, CancellationToken, Task<IEnumerable<OrganizationReportSummaryDataResponse>>>>(),
                options: Arg.Any<FusionCacheEntryOptions>(),
                tags: Arg.Any<IEnumerable<string>>())
            .Returns(callInfo =>
            {
                var factory = callInfo.ArgAt<Func<FusionCacheFactoryExecutionContext<IEnumerable<OrganizationReportSummaryDataResponse>>, CancellationToken, Task<IEnumerable<OrganizationReportSummaryDataResponse>>>>(1);
                return new ValueTask<IEnumerable<OrganizationReportSummaryDataResponse>>(factory.Invoke(null, CancellationToken.None));
            });


        // Act
        var result = await sutProvider.Sut.GetOrganizationReportSummaryDataByDateRangeAsync(organizationId, startDate, endDate);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(6, result.Count());
        await sutProvider.GetDependency<IOrganizationReportRepository>()
            .Received(1).GetSummaryDataByDateRangeAsync(Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<DateTime>());
    }


    [Theory]
    [BitAutoData]
    public async Task GetOrganizationReportSummaryDataByDateRangeAsync_WithEmptyOrganizationId_ShouldThrowBadRequestException(
        SutProvider<GetOrganizationReportSummaryDataByDateRangeQuery> sutProvider)
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var startDate = DateTime.UtcNow.AddDays(-30);
        var endDate = DateTime.UtcNow;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.GetOrganizationReportSummaryDataByDateRangeAsync(Guid.Empty, startDate, endDate));

        Assert.Equal("OrganizationId is required", exception.Message);
        await sutProvider.GetDependency<IOrganizationReportRepository>()
            .DidNotReceive()
            .GetSummaryDataByDateRangeAsync(
                Arg.Any<Guid>(),
                Arg.Any<DateTime>(),
                Arg.Any<DateTime>());
    }

    [Theory]
    [BitAutoData]
    public async Task GetOrganizationReportSummaryDataByDateRangeAsync_WithStartDateAfterEndDate_ShouldThrowBadRequestException(
        SutProvider<GetOrganizationReportSummaryDataByDateRangeQuery> sutProvider)
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var startDate = DateTime.UtcNow;
        var endDate = DateTime.UtcNow.AddDays(-30);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.GetOrganizationReportSummaryDataByDateRangeAsync(organizationId, startDate, endDate));

        Assert.Equal("StartDate must be earlier than or equal to EndDate", exception.Message);
        await sutProvider.GetDependency<IOrganizationReportRepository>()
            .DidNotReceive().GetSummaryDataByDateRangeAsync(Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<DateTime>());
    }

    [Theory]
    [BitAutoData]
    public async Task GetOrganizationReportSummaryDataByDateRangeAsync_WithEmptyResult_ShouldReturnEmptyList(
        SutProvider<GetOrganizationReportSummaryDataByDateRangeQuery> sutProvider)
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var startDate = DateTime.UtcNow.AddDays(-30);
        var endDate = DateTime.UtcNow;

        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetSummaryDataByDateRangeAsync(organizationId, startDate, endDate)
            .Returns(new List<OrganizationReportSummaryDataResponse>());

        sutProvider
            .GetDependency<IFusionCache>()
            .GetOrSetAsync(
                key: Arg.Any<string?>(),
                factory: Arg.Any<Func<object, CancellationToken, Task<IEnumerable<OrganizationReportSummaryDataResponse>>>>(),
                options: Arg.Any<FusionCacheEntryOptions>(),
                tags: Arg.Any<IEnumerable<string>>())
            .Returns(callInfo =>
            {
                var factory = callInfo.ArgAt<Func<FusionCacheFactoryExecutionContext<IEnumerable<OrganizationReportSummaryDataResponse>>, CancellationToken, Task<IEnumerable<OrganizationReportSummaryDataResponse>>>>(1);
                return new ValueTask<IEnumerable<OrganizationReportSummaryDataResponse>>(factory.Invoke(null, CancellationToken.None));
            });


        // Act
        var result = await sutProvider.Sut.GetOrganizationReportSummaryDataByDateRangeAsync(organizationId, startDate, endDate);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Theory]
    [BitAutoData]
    public async Task GetOrganizationReportSummaryDataByDateRangeAsync_WhenRepositoryThrowsException_ShouldPropagateException(
        SutProvider<GetOrganizationReportSummaryDataByDateRangeQuery> sutProvider)
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var startDate = DateTime.UtcNow.AddDays(-30);
        var endDate = DateTime.UtcNow;
        var expectedMessage = "Database connection failed";

        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetSummaryDataByDateRangeAsync(organizationId, startDate, endDate)
            .Throws(new InvalidOperationException(expectedMessage));

        sutProvider
            .GetDependency<IFusionCache>()
            .GetOrSetAsync(
                key: Arg.Any<string?>(),
                factory: Arg.Any<Func<object, CancellationToken, Task<IEnumerable<OrganizationReportSummaryDataResponse>>>>(),
                options: Arg.Any<FusionCacheEntryOptions>(),
                tags: Arg.Any<IEnumerable<string>>())
            .Returns(callInfo =>
            {
                var factory = callInfo.ArgAt<Func<FusionCacheFactoryExecutionContext<IEnumerable<OrganizationReportSummaryDataResponse>>, CancellationToken, Task<IEnumerable<OrganizationReportSummaryDataResponse>>>>(1);
                return new ValueTask<IEnumerable<OrganizationReportSummaryDataResponse>>(factory.Invoke(null, CancellationToken.None));
            });


        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await sutProvider.Sut.GetOrganizationReportSummaryDataByDateRangeAsync(organizationId, startDate, endDate));

        Assert.Equal(expectedMessage, exception.Message);
    }
}
