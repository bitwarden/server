using Bit.Api.Dirt.Controllers;
using Bit.Core.Context;
using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Models.Data;
using Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;
using Bit.Core.Dirt.Reports.ReportFeatures.Requests;
using Bit.Core.Exceptions;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Dirt;

[ControllerCustomize(typeof(OrganizationReportsController))]
[SutProviderCustomize]
public class OrganizationReportControllerTests
{
    #region Whole OrganizationReport Endpoints

    [Theory, BitAutoData]
    public async Task GetLatestOrganizationReportAsync_WithValidOrgId_ReturnsOkResult(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        OrganizationReport expectedReport)
    {
        // Arrange
        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(true);

        sutProvider.GetDependency<IGetOrganizationReportQuery>()
            .GetLatestOrganizationReportAsync(orgId)
            .Returns(expectedReport);

        // Act
        var result = await sutProvider.Sut.GetLatestOrganizationReportAsync(orgId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(expectedReport, okResult.Value);
    }

    [Theory, BitAutoData]
    public async Task GetLatestOrganizationReportAsync_WithoutAccess_ThrowsNotFoundException(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId)
    {
        // Arrange
        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(Task.FromResult(false));

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetLatestOrganizationReportAsync(orgId));

        // Verify that the query was not called
        await sutProvider.GetDependency<IGetOrganizationReportQuery>()
            .DidNotReceive()
            .GetLatestOrganizationReportAsync(Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task GetLatestOrganizationReportAsync_WhenNoReportFound_ReturnsOkWithNull(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId)
    {
        // Arrange
        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(true);

        sutProvider.GetDependency<IGetOrganizationReportQuery>()
            .GetLatestOrganizationReportAsync(orgId)
            .Returns((OrganizationReport)null);

        // Act
        var result = await sutProvider.Sut.GetLatestOrganizationReportAsync(orgId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Null(okResult.Value);
    }

    [Theory, BitAutoData]
    public async Task GetLatestOrganizationReportAsync_CallsCorrectMethods(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        OrganizationReport expectedReport)
    {
        // Arrange
        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(true);

        sutProvider.GetDependency<IGetOrganizationReportQuery>()
            .GetLatestOrganizationReportAsync(orgId)
            .Returns(expectedReport);

        // Act
        await sutProvider.Sut.GetLatestOrganizationReportAsync(orgId);

        // Assert
        await sutProvider.GetDependency<ICurrentContext>()
            .Received(1)
            .AccessReports(orgId);

        await sutProvider.GetDependency<IGetOrganizationReportQuery>()
            .Received(1)
            .GetLatestOrganizationReportAsync(orgId);
    }




    [Theory, BitAutoData]
    public async Task GetOrganizationReportAsync_WithValidIds_ReturnsOkResult(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        Guid reportId,
        OrganizationReport expectedReport)
    {
        // Arrange
        expectedReport.OrganizationId = orgId;
        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(true);

        sutProvider.GetDependency<IGetOrganizationReportQuery>()
            .GetOrganizationReportAsync(reportId)
            .Returns(expectedReport);

        // Act
        var result = await sutProvider.Sut.GetOrganizationReportAsync(orgId, reportId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(expectedReport, okResult.Value);
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationReportAsync_WithoutAccess_ThrowsNotFoundException(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        Guid reportId)
    {
        // Arrange
        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(Task.FromResult(false));

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetOrganizationReportAsync(orgId, reportId));

        // Verify that the query was not called
        await sutProvider.GetDependency<IGetOrganizationReportQuery>()
            .DidNotReceive()
            .GetOrganizationReportAsync(Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationReportAsync_WhenReportNotFound_ThrowsNotFoundException(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        Guid reportId)
    {
        // Arrange
        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(true);

        sutProvider.GetDependency<IGetOrganizationReportQuery>()
            .GetOrganizationReportAsync(reportId)
            .Returns((OrganizationReport)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetOrganizationReportAsync(orgId, reportId));

        Assert.Equal("Report not found for the specified organization.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationReportAsync_CallsCorrectMethods(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        Guid reportId,
        OrganizationReport expectedReport)
    {
        // Arrange
        expectedReport.OrganizationId = orgId;
        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(true);

        sutProvider.GetDependency<IGetOrganizationReportQuery>()
            .GetOrganizationReportAsync(reportId)
            .Returns(expectedReport);

        // Act
        await sutProvider.Sut.GetOrganizationReportAsync(orgId, reportId);

        // Assert
        await sutProvider.GetDependency<ICurrentContext>()
            .Received(1)
            .AccessReports(orgId);

        await sutProvider.GetDependency<IGetOrganizationReportQuery>()
            .Received(1)
            .GetOrganizationReportAsync(reportId);
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationReportAsync_WithValidAccess_UsesCorrectReportId(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        Guid reportId,
        OrganizationReport expectedReport)
    {
        // Arrange
        expectedReport.OrganizationId = orgId;
        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(true);

        sutProvider.GetDependency<IGetOrganizationReportQuery>()
            .GetOrganizationReportAsync(reportId)
            .Returns(expectedReport);

        // Act
        await sutProvider.Sut.GetOrganizationReportAsync(orgId, reportId);

        // Assert
        await sutProvider.GetDependency<IGetOrganizationReportQuery>()
            .Received(1)
            .GetOrganizationReportAsync(reportId);
    }

    [Theory, BitAutoData]
    public async Task CreateOrganizationReportAsync_WithValidRequest_ReturnsOkResult(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        AddOrganizationReportRequest request,
        OrganizationReport expectedReport)
    {
        // Arrange
        request.OrganizationId = orgId;

        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(true);

        sutProvider.GetDependency<IAddOrganizationReportCommand>()
            .AddOrganizationReportAsync(request)
            .Returns(expectedReport);

        // Act
        var result = await sutProvider.Sut.CreateOrganizationReportAsync(orgId, request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(expectedReport, okResult.Value);
    }

    [Theory, BitAutoData]
    public async Task CreateOrganizationReportAsync_WithoutAccess_ThrowsNotFoundException(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        AddOrganizationReportRequest request)
    {
        // Arrange
        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(false);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.CreateOrganizationReportAsync(orgId, request));

        // Verify that the command was not called
        await sutProvider.GetDependency<IAddOrganizationReportCommand>()
            .DidNotReceive()
            .AddOrganizationReportAsync(Arg.Any<AddOrganizationReportRequest>());
    }

    [Theory, BitAutoData]
    public async Task CreateOrganizationReportAsync_WithMismatchedOrgId_ThrowsBadRequestException(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        AddOrganizationReportRequest request)
    {
        // Arrange
        request.OrganizationId = Guid.NewGuid(); // Different from orgId

        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(true);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.CreateOrganizationReportAsync(orgId, request));

        Assert.Equal("Organization ID in the request body must match the route parameter", exception.Message);

        // Verify that the command was not called
        await sutProvider.GetDependency<IAddOrganizationReportCommand>()
            .DidNotReceive()
            .AddOrganizationReportAsync(Arg.Any<AddOrganizationReportRequest>());
    }

    [Theory, BitAutoData]
    public async Task CreateOrganizationReportAsync_CallsCorrectMethods(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        AddOrganizationReportRequest request,
        OrganizationReport expectedReport)
    {
        // Arrange
        request.OrganizationId = orgId;

        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(true);

        sutProvider.GetDependency<IAddOrganizationReportCommand>()
            .AddOrganizationReportAsync(request)
            .Returns(expectedReport);

        // Act
        await sutProvider.Sut.CreateOrganizationReportAsync(orgId, request);

        // Assert
        await sutProvider.GetDependency<ICurrentContext>()
            .Received(1)
            .AccessReports(orgId);

        await sutProvider.GetDependency<IAddOrganizationReportCommand>()
            .Received(1)
            .AddOrganizationReportAsync(request);
    }

    [Theory, BitAutoData]
    public async Task UpdateOrganizationReportAsync_WithValidRequest_ReturnsOkResult(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        UpdateOrganizationReportRequest request,
        OrganizationReport expectedReport)
    {
        // Arrange
        request.OrganizationId = orgId;

        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(true);

        sutProvider.GetDependency<IUpdateOrganizationReportCommand>()
            .UpdateOrganizationReportAsync(request)
            .Returns(expectedReport);

        // Act
        var result = await sutProvider.Sut.UpdateOrganizationReportAsync(orgId, request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(expectedReport, okResult.Value);
    }

    [Theory, BitAutoData]
    public async Task UpdateOrganizationReportAsync_WithoutAccess_ThrowsNotFoundException(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        UpdateOrganizationReportRequest request)
    {
        // Arrange
        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(false);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.UpdateOrganizationReportAsync(orgId, request));

        // Verify that the command was not called
        await sutProvider.GetDependency<IUpdateOrganizationReportCommand>()
            .DidNotReceive()
            .UpdateOrganizationReportAsync(Arg.Any<UpdateOrganizationReportRequest>());
    }

    [Theory, BitAutoData]
    public async Task UpdateOrganizationReportAsync_WithMismatchedOrgId_ThrowsBadRequestException(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        UpdateOrganizationReportRequest request)
    {
        // Arrange
        request.OrganizationId = Guid.NewGuid(); // Different from orgId

        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(true);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.UpdateOrganizationReportAsync(orgId, request));

        Assert.Equal("Organization ID in the request body must match the route parameter", exception.Message);

        // Verify that the command was not called
        await sutProvider.GetDependency<IUpdateOrganizationReportCommand>()
            .DidNotReceive()
            .UpdateOrganizationReportAsync(Arg.Any<UpdateOrganizationReportRequest>());
    }

    [Theory, BitAutoData]
    public async Task UpdateOrganizationReportAsync_CallsCorrectMethods(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        UpdateOrganizationReportRequest request,
        OrganizationReport expectedReport)
    {
        // Arrange
        request.OrganizationId = orgId;

        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(true);

        sutProvider.GetDependency<IUpdateOrganizationReportCommand>()
            .UpdateOrganizationReportAsync(request)
            .Returns(expectedReport);

        // Act
        await sutProvider.Sut.UpdateOrganizationReportAsync(orgId, request);

        // Assert
        await sutProvider.GetDependency<ICurrentContext>()
            .Received(1)
            .AccessReports(orgId);

        await sutProvider.GetDependency<IUpdateOrganizationReportCommand>()
            .Received(1)
            .UpdateOrganizationReportAsync(request);
    }

    #endregion

    #region SummaryData Field Endpoints

    [Theory, BitAutoData]
    public async Task GetOrganizationReportSummaryDataByDateRangeAsync_WithValidParameters_ReturnsOkResult(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        DateTime startDate,
        DateTime endDate,
        List<OrganizationReportSummaryDataResponse> expectedSummaryData)
    {
        // Arrange
        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(true);

        sutProvider.GetDependency<IGetOrganizationReportSummaryDataByDateRangeQuery>()
            .GetOrganizationReportSummaryDataByDateRangeAsync(orgId, startDate, endDate)
            .Returns(expectedSummaryData);

        // Act
        var result = await sutProvider.Sut.GetOrganizationReportSummaryDataByDateRangeAsync(orgId, startDate, endDate);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(expectedSummaryData, okResult.Value);
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationReportSummaryDataByDateRangeAsync_WithoutAccess_ThrowsNotFoundException(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        DateTime startDate,
        DateTime endDate)
    {
        // Arrange
        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(false);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetOrganizationReportSummaryDataByDateRangeAsync(orgId, startDate, endDate));

        // Verify that the query was not called
        await sutProvider.GetDependency<IGetOrganizationReportSummaryDataByDateRangeQuery>()
            .DidNotReceive()
            .GetOrganizationReportSummaryDataByDateRangeAsync(Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<DateTime>());
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationReportSummaryDataByDateRangeAsync_CallsCorrectMethods(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        DateTime startDate,
        DateTime endDate,
        List<OrganizationReportSummaryDataResponse> expectedSummaryData)
    {
        // Arrange
        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(true);

        sutProvider.GetDependency<IGetOrganizationReportSummaryDataByDateRangeQuery>()
            .GetOrganizationReportSummaryDataByDateRangeAsync(orgId, startDate, endDate)
            .Returns(expectedSummaryData);

        // Act
        await sutProvider.Sut.GetOrganizationReportSummaryDataByDateRangeAsync(orgId, startDate, endDate);

        // Assert
        await sutProvider.GetDependency<ICurrentContext>()
            .Received(1)
            .AccessReports(orgId);

        await sutProvider.GetDependency<IGetOrganizationReportSummaryDataByDateRangeQuery>()
            .Received(1)
            .GetOrganizationReportSummaryDataByDateRangeAsync(orgId, startDate, endDate);
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationReportSummaryAsync_WithValidIds_ReturnsOkResult(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        Guid reportId,
        OrganizationReportSummaryDataResponse expectedSummaryData)
    {
        // Arrange
        expectedSummaryData.OrganizationId = orgId;

        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(true);

        sutProvider.GetDependency<IGetOrganizationReportSummaryDataQuery>()
            .GetOrganizationReportSummaryDataAsync(orgId, reportId)
            .Returns(expectedSummaryData);

        // Act
        var result = await sutProvider.Sut.GetOrganizationReportSummaryAsync(orgId, reportId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(expectedSummaryData, okResult.Value);
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationReportSummaryAsync_WithoutAccess_ThrowsNotFoundException(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        Guid reportId)
    {
        // Arrange
        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(false);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetOrganizationReportSummaryAsync(orgId, reportId));

        // Verify that the query was not called
        await sutProvider.GetDependency<IGetOrganizationReportSummaryDataQuery>()
            .DidNotReceive()
            .GetOrganizationReportSummaryDataAsync(Arg.Any<Guid>(), Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationReportSummaryAsync_WithMismatchedOrgId_ThrowsBadRequestException(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        Guid reportId,
        OrganizationReportSummaryDataResponse summaryData)
    {
        // Arrange
        summaryData.OrganizationId = Guid.NewGuid(); // Different from orgId

        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(true);

        sutProvider.GetDependency<IGetOrganizationReportSummaryDataQuery>()
            .GetOrganizationReportSummaryDataAsync(orgId, reportId)
            .Returns(summaryData);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.GetOrganizationReportSummaryAsync(orgId, reportId));

        Assert.Equal("Invalid report ID", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task UpdateOrganizationReportSummaryAsync_WithValidRequest_ReturnsOkResult(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        Guid reportId,
        UpdateOrganizationReportSummaryRequest request,
        OrganizationReport expectedReport)
    {
        // Arrange
        request.OrganizationId = orgId;
        request.ReportId = reportId;

        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(true);

        sutProvider.GetDependency<IUpdateOrganizationReportSummaryCommand>()
            .UpdateOrganizationReportSummaryAsync(request)
            .Returns(expectedReport);

        // Act
        var result = await sutProvider.Sut.UpdateOrganizationReportSummaryAsync(orgId, reportId, request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(expectedReport, okResult.Value);
    }

    [Theory, BitAutoData]
    public async Task UpdateOrganizationReportSummaryAsync_WithoutAccess_ThrowsNotFoundException(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        Guid reportId,
        UpdateOrganizationReportSummaryRequest request)
    {
        // Arrange
        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(false);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.UpdateOrganizationReportSummaryAsync(orgId, reportId, request));

        // Verify that the command was not called
        await sutProvider.GetDependency<IUpdateOrganizationReportSummaryCommand>()
            .DidNotReceive()
            .UpdateOrganizationReportSummaryAsync(Arg.Any<UpdateOrganizationReportSummaryRequest>());
    }

    [Theory, BitAutoData]
    public async Task UpdateOrganizationReportSummaryAsync_WithMismatchedOrgId_ThrowsBadRequestException(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        Guid reportId,
        UpdateOrganizationReportSummaryRequest request)
    {
        // Arrange
        request.OrganizationId = Guid.NewGuid(); // Different from orgId
        request.ReportId = reportId;

        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(true);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.UpdateOrganizationReportSummaryAsync(orgId, reportId, request));

        Assert.Equal("Organization ID in the request body must match the route parameter", exception.Message);

        // Verify that the command was not called
        await sutProvider.GetDependency<IUpdateOrganizationReportSummaryCommand>()
            .DidNotReceive()
            .UpdateOrganizationReportSummaryAsync(Arg.Any<UpdateOrganizationReportSummaryRequest>());
    }

    [Theory, BitAutoData]
    public async Task UpdateOrganizationReportSummaryAsync_WithMismatchedReportId_ThrowsBadRequestException(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        Guid reportId,
        UpdateOrganizationReportSummaryRequest request)
    {
        // Arrange
        request.OrganizationId = orgId;
        request.ReportId = Guid.NewGuid(); // Different from reportId

        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(true);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.UpdateOrganizationReportSummaryAsync(orgId, reportId, request));

        Assert.Equal("Report ID in the request body must match the route parameter", exception.Message);

        // Verify that the command was not called
        await sutProvider.GetDependency<IUpdateOrganizationReportSummaryCommand>()
            .DidNotReceive()
            .UpdateOrganizationReportSummaryAsync(Arg.Any<UpdateOrganizationReportSummaryRequest>());
    }

    [Theory, BitAutoData]
    public async Task UpdateOrganizationReportSummaryAsync_CallsCorrectMethods(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        Guid reportId,
        UpdateOrganizationReportSummaryRequest request,
        OrganizationReport expectedReport)
    {
        // Arrange
        request.OrganizationId = orgId;
        request.ReportId = reportId;

        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(true);

        sutProvider.GetDependency<IUpdateOrganizationReportSummaryCommand>()
            .UpdateOrganizationReportSummaryAsync(request)
            .Returns(expectedReport);

        // Act
        await sutProvider.Sut.UpdateOrganizationReportSummaryAsync(orgId, reportId, request);

        // Assert
        await sutProvider.GetDependency<ICurrentContext>()
            .Received(1)
            .AccessReports(orgId);

        await sutProvider.GetDependency<IUpdateOrganizationReportSummaryCommand>()
            .Received(1)
            .UpdateOrganizationReportSummaryAsync(request);
    }

    #endregion

    #region ReportData Field Endpoints

    [Theory, BitAutoData]
    public async Task GetOrganizationReportDataAsync_WithValidIds_ReturnsOkResult(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        Guid reportId,
        OrganizationReportDataResponse expectedReportData)
    {
        // Arrange
        expectedReportData.OrganizationId = orgId;
        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(true);

        sutProvider.GetDependency<IGetOrganizationReportDataQuery>()
            .GetOrganizationReportDataAsync(orgId, reportId)
            .Returns(expectedReportData);

        // Act
        var result = await sutProvider.Sut.GetOrganizationReportDataAsync(orgId, reportId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(expectedReportData, okResult.Value);
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationReportDataAsync_WithoutAccess_ThrowsNotFoundException(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        Guid reportId)
    {
        // Arrange
        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(false);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetOrganizationReportDataAsync(orgId, reportId));

        // Verify that the query was not called
        await sutProvider.GetDependency<IGetOrganizationReportDataQuery>()
            .DidNotReceive()
            .GetOrganizationReportDataAsync(Arg.Any<Guid>(), Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationReportDataAsync_CallsCorrectMethods(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        Guid reportId,
        OrganizationReportDataResponse expectedReportData)
    {
        // Arrange
        expectedReportData.OrganizationId = orgId;
        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(true);

        sutProvider.GetDependency<IGetOrganizationReportDataQuery>()
            .GetOrganizationReportDataAsync(orgId, reportId)
            .Returns(expectedReportData);

        // Act
        await sutProvider.Sut.GetOrganizationReportDataAsync(orgId, reportId);

        // Assert
        await sutProvider.GetDependency<ICurrentContext>()
            .Received(1)
            .AccessReports(orgId);

        await sutProvider.GetDependency<IGetOrganizationReportDataQuery>()
            .Received(1)
            .GetOrganizationReportDataAsync(orgId, reportId);
    }

    [Theory, BitAutoData]
    public async Task UpdateOrganizationReportDataAsync_WithValidRequest_ReturnsOkResult(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        Guid reportId,
        UpdateOrganizationReportDataRequest request,
        OrganizationReport expectedReport)
    {
        // Arrange
        request.OrganizationId = orgId;
        request.ReportId = reportId;

        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(true);

        sutProvider.GetDependency<IUpdateOrganizationReportDataCommand>()
            .UpdateOrganizationReportDataAsync(request)
            .Returns(expectedReport);

        // Act
        var result = await sutProvider.Sut.UpdateOrganizationReportDataAsync(orgId, reportId, request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(expectedReport, okResult.Value);
    }

    [Theory, BitAutoData]
    public async Task UpdateOrganizationReportDataAsync_WithoutAccess_ThrowsNotFoundException(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        Guid reportId,
        UpdateOrganizationReportDataRequest request)
    {
        // Arrange
        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(false);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.UpdateOrganizationReportDataAsync(orgId, reportId, request));

        // Verify that the command was not called
        await sutProvider.GetDependency<IUpdateOrganizationReportDataCommand>()
            .DidNotReceive()
            .UpdateOrganizationReportDataAsync(Arg.Any<UpdateOrganizationReportDataRequest>());
    }

    [Theory, BitAutoData]
    public async Task UpdateOrganizationReportDataAsync_WithMismatchedOrgId_ThrowsBadRequestException(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        Guid reportId,
        UpdateOrganizationReportDataRequest request)
    {
        // Arrange
        request.OrganizationId = Guid.NewGuid(); // Different from orgId
        request.ReportId = reportId;

        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(true);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.UpdateOrganizationReportDataAsync(orgId, reportId, request));

        Assert.Equal("Organization ID in the request body must match the route parameter", exception.Message);

        // Verify that the command was not called
        await sutProvider.GetDependency<IUpdateOrganizationReportDataCommand>()
            .DidNotReceive()
            .UpdateOrganizationReportDataAsync(Arg.Any<UpdateOrganizationReportDataRequest>());
    }

    [Theory, BitAutoData]
    public async Task UpdateOrganizationReportDataAsync_WithMismatchedReportId_ThrowsBadRequestException(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        Guid reportId,
        UpdateOrganizationReportDataRequest request)
    {
        // Arrange
        request.OrganizationId = orgId;
        request.ReportId = Guid.NewGuid(); // Different from reportId

        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(true);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.UpdateOrganizationReportDataAsync(orgId, reportId, request));

        Assert.Equal("Report ID in the request body must match the route parameter", exception.Message);

        // Verify that the command was not called
        await sutProvider.GetDependency<IUpdateOrganizationReportDataCommand>()
            .DidNotReceive()
            .UpdateOrganizationReportDataAsync(Arg.Any<UpdateOrganizationReportDataRequest>());
    }

    [Theory, BitAutoData]
    public async Task UpdateOrganizationReportDataAsync_CallsCorrectMethods(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        Guid reportId,
        UpdateOrganizationReportDataRequest request,
        OrganizationReport expectedReport)
    {
        // Arrange
        request.OrganizationId = orgId;
        request.ReportId = reportId;

        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(true);

        sutProvider.GetDependency<IUpdateOrganizationReportDataCommand>()
            .UpdateOrganizationReportDataAsync(request)
            .Returns(expectedReport);

        // Act
        await sutProvider.Sut.UpdateOrganizationReportDataAsync(orgId, reportId, request);

        // Assert
        await sutProvider.GetDependency<ICurrentContext>()
            .Received(1)
            .AccessReports(orgId);

        await sutProvider.GetDependency<IUpdateOrganizationReportDataCommand>()
            .Received(1)
            .UpdateOrganizationReportDataAsync(request);
    }

    #endregion

    #region ApplicationData Field Endpoints

    [Theory, BitAutoData]
    public async Task GetOrganizationReportApplicationDataAsync_WithValidIds_ReturnsOkResult(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        Guid reportId,
        OrganizationReportApplicationDataResponse expectedApplicationData)
    {
        // Arrange
        expectedApplicationData.OrganizationId = orgId;
        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(true);

        sutProvider.GetDependency<IGetOrganizationReportApplicationDataQuery>()
            .GetOrganizationReportApplicationDataAsync(orgId, reportId)
            .Returns(expectedApplicationData);

        // Act
        var result = await sutProvider.Sut.GetOrganizationReportApplicationDataAsync(orgId, reportId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(expectedApplicationData, okResult.Value);
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationReportApplicationDataAsync_WithoutAccess_ThrowsNotFoundException(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        Guid reportId)
    {
        // Arrange
        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(false);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetOrganizationReportApplicationDataAsync(orgId, reportId));

        // Verify that the query was not called
        await sutProvider.GetDependency<IGetOrganizationReportApplicationDataQuery>()
            .DidNotReceive()
            .GetOrganizationReportApplicationDataAsync(Arg.Any<Guid>(), Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationReportApplicationDataAsync_WhenApplicationDataNotFound_ThrowsNotFoundException(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        Guid reportId)
    {
        // Arrange
        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(true);

        sutProvider.GetDependency<IGetOrganizationReportApplicationDataQuery>()
            .GetOrganizationReportApplicationDataAsync(orgId, reportId)
            .Returns((OrganizationReportApplicationDataResponse)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetOrganizationReportApplicationDataAsync(orgId, reportId));

        Assert.Equal("Organization report application data not found.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationReportApplicationDataAsync_CallsCorrectMethods(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        Guid reportId,
        OrganizationReportApplicationDataResponse expectedApplicationData)
    {
        // Arrange
        expectedApplicationData.OrganizationId = orgId;

        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(true);

        sutProvider.GetDependency<IGetOrganizationReportApplicationDataQuery>()
            .GetOrganizationReportApplicationDataAsync(orgId, reportId)
            .Returns(expectedApplicationData);

        // Act
        await sutProvider.Sut.GetOrganizationReportApplicationDataAsync(orgId, reportId);

        // Assert
        await sutProvider.GetDependency<ICurrentContext>()
            .Received(1)
            .AccessReports(orgId);

        await sutProvider.GetDependency<IGetOrganizationReportApplicationDataQuery>()
            .Received(1)
            .GetOrganizationReportApplicationDataAsync(orgId, reportId);
    }

    [Theory, BitAutoData]
    public async Task UpdateOrganizationReportApplicationDataAsync_WithValidRequest_ReturnsOkResult(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        UpdateOrganizationReportApplicationDataRequest request,
        OrganizationReport expectedReport)
    {
        // Arrange
        request.OrganizationId = orgId;

        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(true);

        sutProvider.GetDependency<IUpdateOrganizationReportApplicationDataCommand>()
            .UpdateOrganizationReportApplicationDataAsync(request)
            .Returns(expectedReport);

        // Act
        var result = await sutProvider.Sut.UpdateOrganizationReportApplicationDataAsync(orgId, request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(expectedReport, okResult.Value);
    }

    [Theory, BitAutoData]
    public async Task UpdateOrganizationReportApplicationDataAsync_WithoutAccess_ThrowsNotFoundException(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        UpdateOrganizationReportApplicationDataRequest request)
    {
        // Arrange
        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(false);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.UpdateOrganizationReportApplicationDataAsync(orgId, request));

        // Verify that the command was not called
        await sutProvider.GetDependency<IUpdateOrganizationReportApplicationDataCommand>()
            .DidNotReceive()
            .UpdateOrganizationReportApplicationDataAsync(Arg.Any<UpdateOrganizationReportApplicationDataRequest>());
    }

    [Theory, BitAutoData]
    public async Task UpdateOrganizationReportApplicationDataAsync_WithMismatchedOrgId_ThrowsBadRequestException(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        UpdateOrganizationReportApplicationDataRequest request)
    {
        // Arrange
        request.OrganizationId = Guid.NewGuid(); // Different from orgId

        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(true);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.UpdateOrganizationReportApplicationDataAsync(orgId, request));

        Assert.Equal("Organization ID in the request body must match the route parameter", exception.Message);

        // Verify that the command was not called
        await sutProvider.GetDependency<IUpdateOrganizationReportApplicationDataCommand>()
            .DidNotReceive()
            .UpdateOrganizationReportApplicationDataAsync(Arg.Any<UpdateOrganizationReportApplicationDataRequest>());
    }

    [Theory, BitAutoData]
    public async Task UpdateOrganizationReportApplicationDataAsync_CallsCorrectMethods(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        UpdateOrganizationReportApplicationDataRequest request,
        OrganizationReport expectedReport)
    {
        // Arrange
        request.OrganizationId = orgId;

        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(true);

        sutProvider.GetDependency<IUpdateOrganizationReportApplicationDataCommand>()
            .UpdateOrganizationReportApplicationDataAsync(request)
            .Returns(expectedReport);

        // Act
        await sutProvider.Sut.UpdateOrganizationReportApplicationDataAsync(orgId, request);

        // Assert
        await sutProvider.GetDependency<ICurrentContext>()
            .Received(1)
            .AccessReports(orgId);

        await sutProvider.GetDependency<IUpdateOrganizationReportApplicationDataCommand>()
            .Received(1)
            .UpdateOrganizationReportApplicationDataAsync(request);
    }

    #endregion
}
