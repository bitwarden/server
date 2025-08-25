using Bit.Api.Dirt.Controllers;
using Bit.Core.Context;
using Bit.Core.Dirt.Entities;
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
}
