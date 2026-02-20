using Bit.Api.Dirt.Controllers;
using Bit.Api.Dirt.Models.Response;
using Bit.Core.Context;
using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Models.Data;
using Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;
using Bit.Core.Dirt.Reports.ReportFeatures.Requests;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Dirt;

[ControllerCustomize(typeof(OrganizationReportsV2Controller))]
[SutProviderCustomize]
public class OrganizationReportsV2ControllerTests
{
    private static void SetupAuthorization(SutProvider<OrganizationReportsV2Controller> sutProvider,
        Guid orgId, bool accessReports = true, bool useRiskInsights = true)
    {
        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(accessReports);

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(orgId)
            .Returns(new OrganizationAbility { UseRiskInsights = useRiskInsights });
    }

    #region GetOrganizationReportSummaryDataByDateRangeV2Async

    [Theory, BitAutoData]
    public async Task GetOrganizationReportSummaryDataByDateRangeV2Async_WithValidParams_ReturnsList(
        SutProvider<OrganizationReportsV2Controller> sutProvider,
        Guid orgId)
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-30);
        var endDate = DateTime.UtcNow;

        var expectedData = new List<OrganizationReportSummaryDataResponse>
        {
            new() { SummaryData = "summary-1" },
            new() { SummaryData = "summary-2" }
        };

        SetupAuthorization(sutProvider, orgId);

        sutProvider.GetDependency<IGetOrganizationReportSummaryDataByDateRangeV2Query>()
            .GetSummaryDataByDateRangeAsync(orgId, startDate, endDate)
            .Returns(expectedData);

        // Act
        var result = await sutProvider.Sut.GetOrganizationReportSummaryDataByDateRangeV2Async(orgId, startDate, endDate);

        // Assert
        var resultList = result.ToList();
        Assert.Equal(2, resultList.Count);
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationReportSummaryDataByDateRangeV2Async_WithoutAccess_ThrowsNotFoundException(
        SutProvider<OrganizationReportsV2Controller> sutProvider,
        Guid orgId)
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-30);
        var endDate = DateTime.UtcNow;

        SetupAuthorization(sutProvider, orgId, accessReports: false);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetOrganizationReportSummaryDataByDateRangeV2Async(orgId, startDate, endDate));

        // Verify that the query was not called
        await sutProvider.GetDependency<IGetOrganizationReportSummaryDataByDateRangeV2Query>()
            .DidNotReceive()
            .GetSummaryDataByDateRangeAsync(Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<DateTime>());
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationReportSummaryDataByDateRangeV2Async_UseRiskInsightsDisabled_ThrowsBadRequestException(
        SutProvider<OrganizationReportsV2Controller> sutProvider,
        Guid orgId)
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-30);
        var endDate = DateTime.UtcNow;

        SetupAuthorization(sutProvider, orgId, useRiskInsights: false);

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.GetOrganizationReportSummaryDataByDateRangeV2Async(orgId, startDate, endDate));

        // Verify that the query was not called
        await sutProvider.GetDependency<IGetOrganizationReportSummaryDataByDateRangeV2Query>()
            .DidNotReceive()
            .GetSummaryDataByDateRangeAsync(Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<DateTime>());
    }

    #endregion

    #region GetOrganizationReportSummaryV2Async

    [Theory, BitAutoData]
    public async Task GetOrganizationReportSummaryV2Async_WithValidIds_ReturnsResponse(
        SutProvider<OrganizationReportsV2Controller> sutProvider,
        Guid orgId,
        Guid reportId,
        OrganizationReportSummaryDataResponse expectedSummaryData)
    {
        // Arrange
        SetupAuthorization(sutProvider, orgId);

        sutProvider.GetDependency<IGetOrganizationReportSummaryDataV2Query>()
            .GetSummaryDataAsync(orgId, reportId)
            .Returns(expectedSummaryData);

        // Act
        var result = await sutProvider.Sut.GetOrganizationReportSummaryV2Async(orgId, reportId);

        // Assert
        Assert.Equal(expectedSummaryData, result);
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationReportSummaryV2Async_WithoutAccess_ThrowsNotFoundException(
        SutProvider<OrganizationReportsV2Controller> sutProvider,
        Guid orgId,
        Guid reportId)
    {
        // Arrange
        SetupAuthorization(sutProvider, orgId, accessReports: false);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetOrganizationReportSummaryV2Async(orgId, reportId));

        // Verify that the query was not called
        await sutProvider.GetDependency<IGetOrganizationReportSummaryDataV2Query>()
            .DidNotReceive()
            .GetSummaryDataAsync(Arg.Any<Guid>(), Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationReportSummaryV2Async_UseRiskInsightsDisabled_ThrowsBadRequestException(
        SutProvider<OrganizationReportsV2Controller> sutProvider,
        Guid orgId,
        Guid reportId)
    {
        // Arrange
        SetupAuthorization(sutProvider, orgId, useRiskInsights: false);

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.GetOrganizationReportSummaryV2Async(orgId, reportId));

        // Verify that the query was not called
        await sutProvider.GetDependency<IGetOrganizationReportSummaryDataV2Query>()
            .DidNotReceive()
            .GetSummaryDataAsync(Arg.Any<Guid>(), Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationReportSummaryV2Async_NullResult_ThrowsNotFoundException(
        SutProvider<OrganizationReportsV2Controller> sutProvider,
        Guid orgId,
        Guid reportId)
    {
        // Arrange
        SetupAuthorization(sutProvider, orgId);

        sutProvider.GetDependency<IGetOrganizationReportSummaryDataV2Query>()
            .GetSummaryDataAsync(orgId, reportId)
            .Returns((OrganizationReportSummaryDataResponse)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetOrganizationReportSummaryV2Async(orgId, reportId));

        Assert.Equal("Organization report summary data not found.", exception.Message);
    }

    #endregion

    #region UpdateOrganizationReportSummaryV2Async

    [Theory, BitAutoData]
    public async Task UpdateOrganizationReportSummaryV2Async_WithValidRequest_ReturnsResponse(
        SutProvider<OrganizationReportsV2Controller> sutProvider,
        Guid orgId,
        Guid reportId,
        UpdateOrganizationReportSummaryRequest request,
        OrganizationReport expectedReport)
    {
        // Arrange
        request.OrganizationId = orgId;
        request.ReportId = reportId;

        SetupAuthorization(sutProvider, orgId);

        sutProvider.GetDependency<IUpdateOrganizationReportSummaryV2Command>()
            .UpdateSummaryAsync(request)
            .Returns(expectedReport);

        // Act
        var result = await sutProvider.Sut.UpdateOrganizationReportSummaryV2Async(orgId, reportId, request);

        // Assert
        var expectedResponse = new OrganizationReportResponseModel(expectedReport);
        Assert.Equivalent(expectedResponse, result);
    }

    [Theory, BitAutoData]
    public async Task UpdateOrganizationReportSummaryV2Async_WithoutAccess_ThrowsNotFoundException(
        SutProvider<OrganizationReportsV2Controller> sutProvider,
        Guid orgId,
        Guid reportId,
        UpdateOrganizationReportSummaryRequest request)
    {
        // Arrange
        request.OrganizationId = orgId;
        request.ReportId = reportId;

        SetupAuthorization(sutProvider, orgId, accessReports: false);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.UpdateOrganizationReportSummaryV2Async(orgId, reportId, request));

        // Verify that the command was not called
        await sutProvider.GetDependency<IUpdateOrganizationReportSummaryV2Command>()
            .DidNotReceive()
            .UpdateSummaryAsync(Arg.Any<UpdateOrganizationReportSummaryRequest>());
    }

    [Theory, BitAutoData]
    public async Task UpdateOrganizationReportSummaryV2Async_UseRiskInsightsDisabled_ThrowsBadRequestException(
        SutProvider<OrganizationReportsV2Controller> sutProvider,
        Guid orgId,
        Guid reportId,
        UpdateOrganizationReportSummaryRequest request)
    {
        // Arrange
        request.OrganizationId = orgId;
        request.ReportId = reportId;

        SetupAuthorization(sutProvider, orgId, useRiskInsights: false);

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.UpdateOrganizationReportSummaryV2Async(orgId, reportId, request));

        // Verify that the command was not called
        await sutProvider.GetDependency<IUpdateOrganizationReportSummaryV2Command>()
            .DidNotReceive()
            .UpdateSummaryAsync(Arg.Any<UpdateOrganizationReportSummaryRequest>());
    }

    [Theory, BitAutoData]
    public async Task UpdateOrganizationReportSummaryV2Async_MismatchedOrgId_ThrowsBadRequestException(
        SutProvider<OrganizationReportsV2Controller> sutProvider,
        Guid orgId,
        Guid reportId,
        UpdateOrganizationReportSummaryRequest request)
    {
        // Arrange
        request.OrganizationId = Guid.NewGuid(); // Different from orgId
        request.ReportId = reportId;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.UpdateOrganizationReportSummaryV2Async(orgId, reportId, request));

        Assert.Equal("Organization ID in the request body must match the route parameter", exception.Message);

        // Verify that the command was not called
        await sutProvider.GetDependency<IUpdateOrganizationReportSummaryV2Command>()
            .DidNotReceive()
            .UpdateSummaryAsync(Arg.Any<UpdateOrganizationReportSummaryRequest>());
    }

    [Theory, BitAutoData]
    public async Task UpdateOrganizationReportSummaryV2Async_MismatchedReportId_ThrowsBadRequestException(
        SutProvider<OrganizationReportsV2Controller> sutProvider,
        Guid orgId,
        Guid reportId,
        UpdateOrganizationReportSummaryRequest request)
    {
        // Arrange
        request.OrganizationId = orgId;
        request.ReportId = Guid.NewGuid(); // Different from reportId

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.UpdateOrganizationReportSummaryV2Async(orgId, reportId, request));

        Assert.Equal("Report ID in the request body must match the route parameter", exception.Message);

        // Verify that the command was not called
        await sutProvider.GetDependency<IUpdateOrganizationReportSummaryV2Command>()
            .DidNotReceive()
            .UpdateSummaryAsync(Arg.Any<UpdateOrganizationReportSummaryRequest>());
    }

    #endregion
}
