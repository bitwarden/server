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
    public async Task GetLatestOrganizationReportAsync_WithAccess_Success(SutProvider<OrganizationReportsController> sutProvider)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var report = new OrganizationReport { Id = Guid.NewGuid(), OrganizationId = orgId };
        sutProvider.GetDependency<ICurrentContext>().AccessReports(orgId).Returns(true);
        sutProvider.GetDependency<IGetOrganizationReportQuery>()
            .GetLatestOrganizationReportAsync(orgId).Returns(report);

        // Act
        var result = await sutProvider.Sut.GetLatestOrganizationReportAsync(orgId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(report, okResult.Value);
        await sutProvider.GetDependency<IGetOrganizationReportQuery>()
            .Received(1).GetLatestOrganizationReportAsync(orgId);
    }

    [Theory, BitAutoData]
    public async Task GetLatestOrganizationReportAsync_WithoutAccess_ThrowsNotFoundException(SutProvider<OrganizationReportsController> sutProvider)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        sutProvider.GetDependency<ICurrentContext>().AccessReports(orgId).Returns(false);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetLatestOrganizationReportAsync(orgId));

        await sutProvider.GetDependency<IGetOrganizationReportQuery>()
            .DidNotReceive().GetLatestOrganizationReportAsync(Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationReportAsync_WithAccess_ReportExists_Success(SutProvider<OrganizationReportsController> sutProvider)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var report = new OrganizationReport { Id = reportId, OrganizationId = orgId };
        sutProvider.GetDependency<ICurrentContext>().AccessReports(orgId).Returns(true);
        sutProvider.GetDependency<IGetOrganizationReportQuery>()
            .GetOrganizationReportAsync(reportId).Returns(report);

        // Act
        var result = await sutProvider.Sut.GetOrganizationReportAsync(orgId, reportId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(report, okResult.Value);
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationReportAsync_WithAccess_ReportNotFound_ThrowsNotFoundException(SutProvider<OrganizationReportsController> sutProvider)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        sutProvider.GetDependency<ICurrentContext>().AccessReports(orgId).Returns(true);
        sutProvider.GetDependency<IGetOrganizationReportQuery>()
            .GetOrganizationReportAsync(reportId).Returns((OrganizationReport)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetOrganizationReportAsync(orgId, reportId));
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationReportAsync_WithAccess_ReportBelongsToDifferentOrg_ThrowsNotFoundException(SutProvider<OrganizationReportsController> sutProvider)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var differentOrgId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var report = new OrganizationReport { Id = reportId, OrganizationId = differentOrgId };
        sutProvider.GetDependency<ICurrentContext>().AccessReports(orgId).Returns(true);
        sutProvider.GetDependency<IGetOrganizationReportQuery>()
            .GetOrganizationReportAsync(reportId).Returns(report);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetOrganizationReportAsync(orgId, reportId));
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationReportAsync_WithoutAccess_ThrowsNotFoundException(SutProvider<OrganizationReportsController> sutProvider)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        sutProvider.GetDependency<ICurrentContext>().AccessReports(orgId).Returns(false);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetOrganizationReportAsync(orgId, reportId));
    }

    [Theory, BitAutoData]
    public async Task CreateOrganizationReportAsync_WithAccess_ValidRequest_Success(SutProvider<OrganizationReportsController> sutProvider)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var request = new AddOrganizationReportRequest { OrganizationId = orgId };
        var report = new OrganizationReport { Id = Guid.NewGuid(), OrganizationId = orgId };
        sutProvider.GetDependency<ICurrentContext>().AccessReports(orgId).Returns(true);
        sutProvider.GetDependency<IAddOrganizationReportCommand>()
            .AddOrganizationReportAsync(request).Returns(report);

        // Act
        var result = await sutProvider.Sut.CreateOrganizationReportAsync(orgId, request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(report, okResult.Value);
    }

    [Theory, BitAutoData]
    public async Task CreateOrganizationReportAsync_WithAccess_MismatchedOrgId_ThrowsBadRequestException(SutProvider<OrganizationReportsController> sutProvider)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var differentOrgId = Guid.NewGuid();
        var request = new AddOrganizationReportRequest { OrganizationId = differentOrgId };
        sutProvider.GetDependency<ICurrentContext>().AccessReports(orgId).Returns(true);

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.CreateOrganizationReportAsync(orgId, request));
    }

    [Theory, BitAutoData]
    public async Task CreateOrganizationReportAsync_WithoutAccess_ThrowsNotFoundException(SutProvider<OrganizationReportsController> sutProvider)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var request = new AddOrganizationReportRequest { OrganizationId = orgId };
        sutProvider.GetDependency<ICurrentContext>().AccessReports(orgId).Returns(false);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.CreateOrganizationReportAsync(orgId, request));
    }

    [Theory, BitAutoData]
    public async Task UpdateOrganizationReportAsync_WithAccess_ValidRequest_Success(SutProvider<OrganizationReportsController> sutProvider)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var request = new UpdateOrganizationReportRequest { OrganizationId = orgId };
        var report = new OrganizationReport { Id = Guid.NewGuid(), OrganizationId = orgId };
        sutProvider.GetDependency<ICurrentContext>().AccessReports(orgId).Returns(true);
        sutProvider.GetDependency<IUpdateOrganizationReportCommand>()
            .UpdateOrganizationReportAsync(request).Returns(report);

        // Act
        var result = await sutProvider.Sut.UpdateOrganizationReportAsync(orgId, request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(report, okResult.Value);
    }

    [Theory, BitAutoData]
    public async Task UpdateOrganizationReportAsync_WithAccess_MismatchedOrgId_ThrowsBadRequestException(SutProvider<OrganizationReportsController> sutProvider)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var differentOrgId = Guid.NewGuid();
        var request = new UpdateOrganizationReportRequest { OrganizationId = differentOrgId };
        sutProvider.GetDependency<ICurrentContext>().AccessReports(orgId).Returns(true);

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.UpdateOrganizationReportAsync(orgId, request));
    }

    [Theory, BitAutoData]
    public async Task UpdateOrganizationReportAsync_WithoutAccess_ThrowsNotFoundException(SutProvider<OrganizationReportsController> sutProvider)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var request = new UpdateOrganizationReportRequest { OrganizationId = orgId };
        sutProvider.GetDependency<ICurrentContext>().AccessReports(orgId).Returns(false);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.UpdateOrganizationReportAsync(orgId, request));
    }

    #endregion

    #region SummaryData Field Endpoints

    [Theory, BitAutoData]
    public async Task GetOrganizationReportSummaryDataByDateRangeAsync_WithAccess_Success(SutProvider<OrganizationReportsController> sutProvider)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var startDate = DateTime.UtcNow.AddDays(-30);
        var endDate = DateTime.UtcNow;
        var summaryDataList = new List<OrganizationReportSummaryDataResponse>
        {
            new OrganizationReportSummaryDataResponse { Id = Guid.NewGuid(), OrganizationId = orgId }
        };

        sutProvider.GetDependency<ICurrentContext>().AccessReports(orgId).Returns(true);
        sutProvider.GetDependency<IGetOrganizationReportSummaryDataByDateRangeQuery>()
            .GetOrganizationReportSummaryDataByDateRangeAsync(orgId, reportId, startDate, endDate)
            .Returns(summaryDataList);

        // Act
        var result = await sutProvider.Sut.GetOrganizationReportSummaryDataByDateRangeAsync(orgId, reportId, startDate, endDate);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(summaryDataList, okResult.Value);
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationReportSummaryDataByDateRangeAsync_WithoutAccess_ThrowsNotFoundException(SutProvider<OrganizationReportsController> sutProvider)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var startDate = DateTime.UtcNow.AddDays(-30);
        var endDate = DateTime.UtcNow;
        sutProvider.GetDependency<ICurrentContext>().AccessReports(orgId).Returns(false);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetOrganizationReportSummaryDataByDateRangeAsync(orgId, reportId, startDate, endDate));
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationReportSummaryAsync_WithAccess_ReportExists_Success(SutProvider<OrganizationReportsController> sutProvider)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var summaryData = new OrganizationReportSummaryDataResponse { Id = reportId, OrganizationId = orgId };

        sutProvider.GetDependency<ICurrentContext>().AccessReports(orgId).Returns(true);
        sutProvider.GetDependency<IGetOrganizationReportSummaryDataQuery>()
            .GetOrganizationReportSummaryDataAsync(orgId, reportId).Returns(summaryData);

        // Act
        var result = await sutProvider.Sut.GetOrganizationReportSummaryAsync(orgId, reportId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(summaryData, okResult.Value);
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationReportSummaryAsync_WithAccess_ReportNotFound_ThrowsNotFoundException(SutProvider<OrganizationReportsController> sutProvider)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var reportId = Guid.NewGuid();

        sutProvider.GetDependency<ICurrentContext>().AccessReports(orgId).Returns(true);
        sutProvider.GetDependency<IGetOrganizationReportSummaryDataQuery>()
            .GetOrganizationReportSummaryDataAsync(orgId, reportId).Returns((OrganizationReportSummaryDataResponse)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetOrganizationReportSummaryAsync(orgId, reportId));
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationReportSummaryAsync_WithAccess_ReportBelongsToDifferentOrg_ThrowsNotFoundException(SutProvider<OrganizationReportsController> sutProvider)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var differentOrgId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var summaryData = new OrganizationReportSummaryDataResponse { Id = reportId, OrganizationId = differentOrgId };

        sutProvider.GetDependency<ICurrentContext>().AccessReports(orgId).Returns(true);
        sutProvider.GetDependency<IGetOrganizationReportSummaryDataQuery>()
            .GetOrganizationReportSummaryDataAsync(orgId, reportId).Returns(summaryData);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetOrganizationReportSummaryAsync(orgId, reportId));
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationReportSummaryAsync_WithoutAccess_ThrowsNotFoundException(SutProvider<OrganizationReportsController> sutProvider)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        sutProvider.GetDependency<ICurrentContext>().AccessReports(orgId).Returns(false);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetOrganizationReportSummaryAsync(orgId, reportId));
    }

    [Theory, BitAutoData]
    public async Task UpdateOrganizationReportSummaryAsync_WithAccess_ValidRequest_Success(SutProvider<OrganizationReportsController> sutProvider)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var request = new UpdateOrganizationReportSummaryRequest { OrganizationId = orgId };
        var report = new OrganizationReport { Id = Guid.NewGuid(), OrganizationId = orgId };

        sutProvider.GetDependency<ICurrentContext>().AccessReports(orgId).Returns(true);
        sutProvider.GetDependency<IUpdateOrganizationReportSummaryCommand>()
            .UpdateOrganizationReportSummaryAsync(request).Returns(report);

        // Act
        var result = await sutProvider.Sut.UpdateOrganizationReportSummaryAsync(orgId, request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(report, okResult.Value);
    }

    [Theory, BitAutoData]
    public async Task UpdateOrganizationReportSummaryAsync_WithAccess_MismatchedOrgId_ThrowsBadRequestException(SutProvider<OrganizationReportsController> sutProvider)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var differentOrgId = Guid.NewGuid();
        var request = new UpdateOrganizationReportSummaryRequest { OrganizationId = differentOrgId };

        sutProvider.GetDependency<ICurrentContext>().AccessReports(differentOrgId).Returns(true);

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.UpdateOrganizationReportSummaryAsync(orgId, request));
    }

    [Theory, BitAutoData]
    public async Task UpdateOrganizationReportSummaryAsync_WithoutAccess_ThrowsNotFoundException(SutProvider<OrganizationReportsController> sutProvider)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var request = new UpdateOrganizationReportSummaryRequest { OrganizationId = orgId };
        sutProvider.GetDependency<ICurrentContext>().AccessReports(orgId).Returns(false);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.UpdateOrganizationReportSummaryAsync(orgId, request));
    }

    #endregion

    #region ReportData Field Endpoints

    [Theory, BitAutoData]
    public async Task GetOrganizationReportDataAsync_WithAccess_Success(SutProvider<OrganizationReportsController> sutProvider)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var reportData = new OrganizationReportDataResponse { Id = reportId, OrganizationId = orgId, ReportData = "test data" };

        sutProvider.GetDependency<ICurrentContext>().AccessReports(orgId).Returns(true);
        sutProvider.GetDependency<IGetOrganizationReportDataQuery>()
            .GetOrganizationReportDataAsync(orgId, reportId).Returns(reportData);

        // Act
        var result = await sutProvider.Sut.GetOrganizationReportDataAsync(orgId, reportId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(reportData, okResult.Value);
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationReportDataAsync_WithoutAccess_ThrowsNotFoundException(SutProvider<OrganizationReportsController> sutProvider)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        sutProvider.GetDependency<ICurrentContext>().AccessReports(orgId).Returns(false);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetOrganizationReportDataAsync(orgId, reportId));
    }

    [Theory, BitAutoData]
    public async Task UpdateOrganizationReportDataAsync_WithAccess_ValidRequest_Success(SutProvider<OrganizationReportsController> sutProvider)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var request = new UpdateOrganizationReportDataRequest { OrganizationId = orgId, ReportId = reportId };
        var report = new OrganizationReport { Id = reportId, OrganizationId = orgId };

        sutProvider.GetDependency<ICurrentContext>().AccessReports(orgId).Returns(true);
        sutProvider.GetDependency<IUpdateOrganizationReportDataCommand>()
            .UpdateOrganizationReportDataAsync(request).Returns(report);

        // Act
        var result = await sutProvider.Sut.UpdateOrganizationReportDataAsync(orgId, reportId, request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(report, okResult.Value);
    }

    [Theory, BitAutoData]
    public async Task UpdateOrganizationReportDataAsync_WithAccess_MismatchedOrgId_ThrowsBadRequestException(SutProvider<OrganizationReportsController> sutProvider)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var differentOrgId = Guid.NewGuid();
        var request = new UpdateOrganizationReportDataRequest { OrganizationId = differentOrgId, ReportId = reportId };

        sutProvider.GetDependency<ICurrentContext>().AccessReports(orgId).Returns(true);

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.UpdateOrganizationReportDataAsync(orgId, reportId, request));
    }

    [Theory, BitAutoData]
    public async Task UpdateOrganizationReportDataAsync_WithAccess_MismatchedReportId_ThrowsBadRequestException(SutProvider<OrganizationReportsController> sutProvider)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var differentReportId = Guid.NewGuid();
        var request = new UpdateOrganizationReportDataRequest { OrganizationId = orgId, ReportId = differentReportId };

        sutProvider.GetDependency<ICurrentContext>().AccessReports(orgId).Returns(true);

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.UpdateOrganizationReportDataAsync(orgId, reportId, request));
    }

    [Theory, BitAutoData]
    public async Task UpdateOrganizationReportDataAsync_WithoutAccess_ThrowsNotFoundException(SutProvider<OrganizationReportsController> sutProvider)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var request = new UpdateOrganizationReportDataRequest { OrganizationId = orgId, ReportId = reportId };
        sutProvider.GetDependency<ICurrentContext>().AccessReports(orgId).Returns(false);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.UpdateOrganizationReportDataAsync(orgId, reportId, request));
    }

    #endregion

    #region ApplicationData Field Endpoints

    [Theory, BitAutoData]
    public async Task GetOrganizationReportApplicationDataAsync_WithAccess_ReportExists_Success(SutProvider<OrganizationReportsController> sutProvider)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var applicationData = new OrganizationReportApplicationDataResponse
        {
            Id = reportId,
            OrganizationId = orgId,
            ApplicationData = "test application data"
        };

        sutProvider.GetDependency<ICurrentContext>().AccessReports(orgId).Returns(true);
        sutProvider.GetDependency<IGetOrganizationReportApplicationDataQuery>()
            .GetOrganizationReportApplicationDataAsync(orgId, reportId).Returns(applicationData);

        // Act
        var result = await sutProvider.Sut.GetOrganizationReportApplicationDataAsync(orgId, reportId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(applicationData, okResult.Value);
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationReportApplicationDataAsync_WithAccess_ReportNotFound_ThrowsNotFoundException(SutProvider<OrganizationReportsController> sutProvider)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var reportId = Guid.NewGuid();

        sutProvider.GetDependency<ICurrentContext>().AccessReports(orgId).Returns(true);
        sutProvider.GetDependency<IGetOrganizationReportApplicationDataQuery>()
            .GetOrganizationReportApplicationDataAsync(orgId, reportId).Returns((OrganizationReportApplicationDataResponse)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetOrganizationReportApplicationDataAsync(orgId, reportId));
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationReportApplicationDataAsync_WithAccess_ReportBelongsToDifferentOrg_ThrowsNotFoundException(SutProvider<OrganizationReportsController> sutProvider)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var differentOrgId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var applicationData = new OrganizationReportApplicationDataResponse
        {
            Id = reportId,
            OrganizationId = differentOrgId,
            ApplicationData = "test application data"
        };

        sutProvider.GetDependency<ICurrentContext>().AccessReports(orgId).Returns(true);
        sutProvider.GetDependency<IGetOrganizationReportApplicationDataQuery>()
            .GetOrganizationReportApplicationDataAsync(orgId, reportId).Returns(applicationData);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetOrganizationReportApplicationDataAsync(orgId, reportId));
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationReportApplicationDataAsync_WithoutAccess_ThrowsNotFoundException(SutProvider<OrganizationReportsController> sutProvider)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        sutProvider.GetDependency<ICurrentContext>().AccessReports(orgId).Returns(false);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetOrganizationReportApplicationDataAsync(orgId, reportId));
    }

    [Theory, BitAutoData]
    public async Task UpdateOrganizationReportApplicationDataAsync_WithAccess_ValidRequest_Success(SutProvider<OrganizationReportsController> sutProvider)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var request = new UpdateOrganizationReportApplicationDataRequest
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            ApplicationData = "updated application data"
        };
        var report = new OrganizationReport { Id = request.Id, OrganizationId = orgId };

        sutProvider.GetDependency<ICurrentContext>().AccessReports(orgId).Returns(true);
        sutProvider.GetDependency<IUpdateOrganizationReportApplicationDataCommand>()
            .UpdateOrganizationReportApplicationDataAsync(request).Returns(report);

        // Act
        var result = await sutProvider.Sut.UpdateOrganizationReportApplicationDataAsync(orgId, request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(report, okResult.Value);
    }

    [Theory, BitAutoData]
    public async Task UpdateOrganizationReportApplicationDataAsync_WithAccess_MismatchedOrgId_ThrowsBadRequestException(SutProvider<OrganizationReportsController> sutProvider)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var differentOrgId = Guid.NewGuid();
        var request = new UpdateOrganizationReportApplicationDataRequest
        {
            Id = Guid.NewGuid(),
            OrganizationId = differentOrgId,
            ApplicationData = "application data"
        };

        sutProvider.GetDependency<ICurrentContext>().AccessReports(orgId).Returns(true);

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.UpdateOrganizationReportApplicationDataAsync(orgId, request));
    }

    [Theory, BitAutoData]
    public async Task UpdateOrganizationReportApplicationDataAsync_WithoutAccess_ThrowsNotFoundException(SutProvider<OrganizationReportsController> sutProvider)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var request = new UpdateOrganizationReportApplicationDataRequest
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            ApplicationData = "application data"
        };
        sutProvider.GetDependency<ICurrentContext>().AccessReports(orgId).Returns(false);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.UpdateOrganizationReportApplicationDataAsync(orgId, request));
    }

    #endregion
}
