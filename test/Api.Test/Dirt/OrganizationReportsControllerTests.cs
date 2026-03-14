using Bit.Api.Dirt.Controllers;
using Bit.Api.Dirt.Models.Request;
using Bit.Api.Dirt.Models.Response;
using Bit.Core;
using Bit.Core.Context;
using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Models.Data;
using Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;
using Bit.Core.Dirt.Reports.ReportFeatures.Requests;
using Bit.Core.Dirt.Reports.Services;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Services;
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
    // GetLatestOrganizationReportAsync - V1 (flag off)

    [Theory, BitAutoData]
    public async Task GetLatestOrganizationReportAsync_V1_WithValidOrgId_ReturnsOkResult(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        OrganizationReport expectedReport)
    {
        // Arrange
        expectedReport.ReportFile = null;

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.AccessIntelligenceVersion2)
            .Returns(false);

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
        var expectedResponse = new OrganizationReportResponseModel(expectedReport);
        Assert.Equivalent(expectedResponse, okResult.Value);
    }

    [Theory, BitAutoData]
    public async Task GetLatestOrganizationReportAsync_V1_WithoutAccess_ThrowsNotFoundException(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.AccessIntelligenceVersion2)
            .Returns(false);

        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(false);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetLatestOrganizationReportAsync(orgId));

        await sutProvider.GetDependency<IGetOrganizationReportQuery>()
            .DidNotReceive()
            .GetLatestOrganizationReportAsync(Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task GetLatestOrganizationReportAsync_V1_WhenNoReportFound_ReturnsOkWithNull(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.AccessIntelligenceVersion2)
            .Returns(false);

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

    // GetLatestOrganizationReportAsync - V2 (flag on)

    [Theory, BitAutoData]
    public async Task GetLatestOrganizationReportAsync_V2_WithValidatedFile_ReturnsOkWithDownloadUrl(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        OrganizationReport expectedReport,
        string downloadUrl)
    {
        // Arrange
        var reportFile = new ReportFile { Id = "file-id", FileName = "report.json", Size = 1024, Validated = true };
        expectedReport.SetReportFile(reportFile);

        SetupV2Authorization(sutProvider, orgId);

        sutProvider.GetDependency<IGetOrganizationReportQuery>()
            .GetLatestOrganizationReportAsync(orgId)
            .Returns(expectedReport);

        sutProvider.GetDependency<IOrganizationReportStorageService>()
            .GetReportDataDownloadUrlAsync(expectedReport, Arg.Any<ReportFile>())
            .Returns(downloadUrl);

        // Act
        var result = await sutProvider.Sut.GetLatestOrganizationReportAsync(orgId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<OrganizationReportResponseModel>(okResult.Value);
        Assert.Equal(downloadUrl, response.ReportFileDownloadUrl);
    }

    [Theory, BitAutoData]
    public async Task GetLatestOrganizationReportAsync_V2_WithNoFile_ReturnsOkWithNullDownloadUrl(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        OrganizationReport expectedReport)
    {
        // Arrange
        expectedReport.ReportFile = null;

        SetupV2Authorization(sutProvider, orgId);

        sutProvider.GetDependency<IGetOrganizationReportQuery>()
            .GetLatestOrganizationReportAsync(orgId)
            .Returns(expectedReport);

        // Act
        var result = await sutProvider.Sut.GetLatestOrganizationReportAsync(orgId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<OrganizationReportResponseModel>(okResult.Value);
        Assert.Null(response.ReportFileDownloadUrl);
    }

    [Theory, BitAutoData]
    public async Task GetLatestOrganizationReportAsync_V2_NoReport_ReturnsOkWithNull(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId)
    {
        // Arrange
        SetupV2Authorization(sutProvider, orgId);

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
    public async Task GetLatestOrganizationReportAsync_V2_WithoutAccess_ThrowsNotFoundException(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.AccessIntelligenceVersion2)
            .Returns(true);

        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(false);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetLatestOrganizationReportAsync(orgId));

        await sutProvider.GetDependency<IGetOrganizationReportQuery>()
            .DidNotReceive()
            .GetLatestOrganizationReportAsync(Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task GetLatestOrganizationReportAsync_V2_NoUseRiskInsights_ThrowsBadRequestException(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.AccessIntelligenceVersion2)
            .Returns(true);

        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(true);

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(orgId)
            .Returns(new OrganizationAbility { UseRiskInsights = false });

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.GetLatestOrganizationReportAsync(orgId));

        await sutProvider.GetDependency<IGetOrganizationReportQuery>()
            .DidNotReceive()
            .GetLatestOrganizationReportAsync(Arg.Any<Guid>());
    }

    // CreateOrganizationReportAsync - V1 (flag off)

    [Theory, BitAutoData]
    public async Task CreateOrganizationReportAsync_V1_WithValidRequest_ReturnsOkResult(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        AddOrganizationReportRequestModel request,
        OrganizationReport expectedReport)
    {
        // Arrange
        expectedReport.ReportFile = null;

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.AccessIntelligenceVersion2)
            .Returns(false);

        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(true);

        sutProvider.GetDependency<IAddOrganizationReportCommand>()
            .AddOrganizationReportAsync(Arg.Any<AddOrganizationReportRequest>())
            .Returns(expectedReport);

        // Act
        var result = await sutProvider.Sut.CreateOrganizationReportAsync(orgId, request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var expectedResponse = new OrganizationReportResponseModel(expectedReport);
        Assert.Equivalent(expectedResponse, okResult.Value);
    }

    [Theory, BitAutoData]
    public async Task CreateOrganizationReportAsync_V1_WithoutAccess_ThrowsNotFoundException(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        AddOrganizationReportRequestModel request)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.AccessIntelligenceVersion2)
            .Returns(false);

        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(false);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.CreateOrganizationReportAsync(orgId, request));

        await sutProvider.GetDependency<IAddOrganizationReportCommand>()
            .DidNotReceive()
            .AddOrganizationReportAsync(Arg.Any<AddOrganizationReportRequest>());
    }

    // CreateOrganizationReportAsync - V2 (flag on)

    [Theory, BitAutoData]
    public async Task CreateOrganizationReportAsync_V2_WithValidRequest_ReturnsFileResponseModel(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        AddOrganizationReportRequestModel request,
        OrganizationReport expectedReport,
        string uploadUrl)
    {
        // Arrange
        request.FileSize = 1024;

        var reportFile = new ReportFile { Id = "file-id", FileName = "report.json", Size = 1024, Validated = false };
        expectedReport.SetReportFile(reportFile);

        SetupV2Authorization(sutProvider, orgId);

        sutProvider.GetDependency<ICreateOrganizationReportCommand>()
            .CreateAsync(Arg.Any<AddOrganizationReportRequest>())
            .Returns(expectedReport);

        sutProvider.GetDependency<IOrganizationReportStorageService>()
            .GetReportFileUploadUrlAsync(expectedReport, Arg.Any<ReportFile>())
            .Returns(uploadUrl);

        sutProvider.GetDependency<IOrganizationReportStorageService>()
            .FileUploadType
            .Returns(FileUploadType.Azure);

        // Act
        var result = await sutProvider.Sut.CreateOrganizationReportAsync(orgId, request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<OrganizationReportFileResponseModel>(okResult.Value);
        Assert.Equal(uploadUrl, response.ReportFileUploadUrl);
        Assert.Equal(FileUploadType.Azure, response.FileUploadType);
        Assert.NotNull(response.ReportResponse);
    }

    [Theory, BitAutoData]
    public async Task CreateOrganizationReportAsync_V2_EmptyOrgId_ThrowsBadRequestException(
        SutProvider<OrganizationReportsController> sutProvider,
        AddOrganizationReportRequestModel request)
    {
        // Arrange
        var emptyOrgId = Guid.Empty;

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.AccessIntelligenceVersion2)
            .Returns(true);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.CreateOrganizationReportAsync(emptyOrgId, request));

        Assert.Equal("Organization ID is required.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task CreateOrganizationReportAsync_V2_MissingFileSize_ThrowsBadRequestException(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        AddOrganizationReportRequestModel request)
    {
        // Arrange
        request.FileSize = null;

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.AccessIntelligenceVersion2)
            .Returns(true);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.CreateOrganizationReportAsync(orgId, request));

        Assert.Equal("File size is required.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task CreateOrganizationReportAsync_V2_WithoutAccess_ThrowsNotFoundException(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        AddOrganizationReportRequestModel request)
    {
        // Arrange
        request.FileSize = 1024;

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.AccessIntelligenceVersion2)
            .Returns(true);

        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(false);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.CreateOrganizationReportAsync(orgId, request));

        await sutProvider.GetDependency<ICreateOrganizationReportCommand>()
            .DidNotReceive()
            .CreateAsync(Arg.Any<AddOrganizationReportRequest>());
    }

    // GetOrganizationReportAsync - V1 (flag off)

    [Theory, BitAutoData]
    public async Task GetOrganizationReportAsync_V1_WithValidIds_ReturnsOkResult(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        Guid reportId,
        OrganizationReport expectedReport)
    {
        // Arrange
        expectedReport.OrganizationId = orgId;
        expectedReport.ReportFile = null;

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.AccessIntelligenceVersion2)
            .Returns(false);

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
        var response = Assert.IsType<OrganizationReportResponseModel>(okResult.Value);
        Assert.Equal(expectedReport.Id, response.Id);
        Assert.Equal(expectedReport.OrganizationId, response.OrganizationId);
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationReportAsync_V1_WithoutAccess_ThrowsNotFoundException(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        Guid reportId)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.AccessIntelligenceVersion2)
            .Returns(false);

        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(false);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetOrganizationReportAsync(orgId, reportId));

        await sutProvider.GetDependency<IGetOrganizationReportQuery>()
            .DidNotReceive()
            .GetOrganizationReportAsync(Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationReportAsync_V1_WhenReportNotFound_ThrowsNotFoundException(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        Guid reportId)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.AccessIntelligenceVersion2)
            .Returns(false);

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
    public async Task GetOrganizationReportAsync_V1_WithOrgMismatch_ThrowsBadRequestException(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        Guid reportId,
        OrganizationReport expectedReport)
    {
        // Arrange
        expectedReport.OrganizationId = Guid.NewGuid();

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.AccessIntelligenceVersion2)
            .Returns(false);

        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(true);

        sutProvider.GetDependency<IGetOrganizationReportQuery>()
            .GetOrganizationReportAsync(reportId)
            .Returns(expectedReport);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.GetOrganizationReportAsync(orgId, reportId));

        Assert.Equal("Invalid report ID", exception.Message);
    }

    // GetOrganizationReportAsync - V2 (flag on)

    [Theory, BitAutoData]
    public async Task GetOrganizationReportAsync_V2_WithValidatedFile_ReturnsOkWithDownloadUrl(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        Guid reportId,
        OrganizationReport expectedReport,
        string downloadUrl)
    {
        // Arrange
        expectedReport.OrganizationId = orgId;
        var reportFile = new ReportFile { Id = "file-id", FileName = "report.json", Size = 1024, Validated = true };
        expectedReport.SetReportFile(reportFile);

        SetupV2Authorization(sutProvider, orgId);

        sutProvider.GetDependency<IGetOrganizationReportQuery>()
            .GetOrganizationReportAsync(reportId)
            .Returns(expectedReport);

        sutProvider.GetDependency<IOrganizationReportStorageService>()
            .GetReportDataDownloadUrlAsync(expectedReport, Arg.Any<ReportFile>())
            .Returns(downloadUrl);

        // Act
        var result = await sutProvider.Sut.GetOrganizationReportAsync(orgId, reportId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<OrganizationReportResponseModel>(okResult.Value);
        Assert.Equal(downloadUrl, response.ReportFileDownloadUrl);
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationReportAsync_V2_WithNoFile_ReturnsOkWithoutDownloadUrl(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        Guid reportId,
        OrganizationReport expectedReport)
    {
        // Arrange
        expectedReport.OrganizationId = orgId;
        expectedReport.ReportFile = null;

        SetupV2Authorization(sutProvider, orgId);

        sutProvider.GetDependency<IGetOrganizationReportQuery>()
            .GetOrganizationReportAsync(reportId)
            .Returns(expectedReport);

        // Act
        var result = await sutProvider.Sut.GetOrganizationReportAsync(orgId, reportId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<OrganizationReportResponseModel>(okResult.Value);
        Assert.Null(response.ReportFileDownloadUrl);
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationReportAsync_V2_WithOrgMismatch_ThrowsBadRequestException(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        Guid reportId,
        OrganizationReport expectedReport)
    {
        // Arrange
        expectedReport.OrganizationId = Guid.NewGuid();

        SetupV2Authorization(sutProvider, orgId);

        sutProvider.GetDependency<IGetOrganizationReportQuery>()
            .GetOrganizationReportAsync(reportId)
            .Returns(expectedReport);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.GetOrganizationReportAsync(orgId, reportId));

        Assert.Equal("Invalid report ID", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationReportAsync_V2_WithoutAccess_ThrowsNotFoundException(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        Guid reportId)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.AccessIntelligenceVersion2)
            .Returns(true);

        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(false);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetOrganizationReportAsync(orgId, reportId));

        await sutProvider.GetDependency<IGetOrganizationReportQuery>()
            .DidNotReceive()
            .GetOrganizationReportAsync(Arg.Any<Guid>());
    }

    // UpdateOrganizationReportAsync - V1 (flag off)

    [Theory, BitAutoData]
    public async Task UpdateOrganizationReportAsync_V1_WithValidRequest_ReturnsOkResult(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        Guid reportId,
        UpdateOrganizationReportV2RequestModel request,
        OrganizationReport expectedReport)
    {
        // Arrange
        expectedReport.ReportFile = null;

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.AccessIntelligenceVersion2)
            .Returns(false);

        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(true);

        sutProvider.GetDependency<IUpdateOrganizationReportCommand>()
            .UpdateOrganizationReportAsync(Arg.Any<UpdateOrganizationReportRequest>())
            .Returns(expectedReport);

        // Act
        var result = await sutProvider.Sut.UpdateOrganizationReportAsync(orgId, reportId, request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var expectedResponse = new OrganizationReportResponseModel(expectedReport);
        Assert.Equivalent(expectedResponse, okResult.Value);

        await sutProvider.GetDependency<IUpdateOrganizationReportCommand>()
            .Received(1)
            .UpdateOrganizationReportAsync(Arg.Is<UpdateOrganizationReportRequest>(r =>
                r.OrganizationId == orgId && r.ReportId == reportId));
    }

    [Theory, BitAutoData]
    public async Task UpdateOrganizationReportAsync_V1_WithoutAccess_ThrowsNotFoundException(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        Guid reportId,
        UpdateOrganizationReportV2RequestModel request)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.AccessIntelligenceVersion2)
            .Returns(false);

        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(false);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.UpdateOrganizationReportAsync(orgId, reportId, request));

        await sutProvider.GetDependency<IUpdateOrganizationReportCommand>()
            .DidNotReceive()
            .UpdateOrganizationReportAsync(Arg.Any<UpdateOrganizationReportRequest>());
    }

    // UpdateOrganizationReportAsync - V2 (flag on)

    [Theory, BitAutoData]
    public async Task UpdateOrganizationReportAsync_V2_NoNewFileUpload_ReturnsReportResponseModel(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        Guid reportId,
        UpdateOrganizationReportV2RequestModel request,
        OrganizationReport expectedReport)
    {
        // Arrange
        request.RequiresNewFileUpload = false;
        expectedReport.ReportFile = null;

        SetupV2Authorization(sutProvider, orgId);

        sutProvider.GetDependency<IUpdateOrganizationReportV2Command>()
            .UpdateAsync(Arg.Any<UpdateOrganizationReportV2Request>())
            .Returns(expectedReport);

        // Act
        var result = await sutProvider.Sut.UpdateOrganizationReportAsync(orgId, reportId, request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.IsType<OrganizationReportResponseModel>(okResult.Value);

        await sutProvider.GetDependency<IUpdateOrganizationReportV2Command>()
            .Received(1)
            .UpdateAsync(Arg.Any<UpdateOrganizationReportV2Request>());
    }

    [Theory, BitAutoData]
    public async Task UpdateOrganizationReportAsync_V2_WithNewFileUpload_ReturnsFileResponseModel(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        Guid reportId,
        UpdateOrganizationReportV2RequestModel request,
        OrganizationReport expectedReport,
        string uploadUrl)
    {
        // Arrange
        request.RequiresNewFileUpload = true;

        var reportFile = new ReportFile { Id = "file-id", FileName = "report.json", Size = 1024, Validated = false };
        expectedReport.SetReportFile(reportFile);

        SetupV2Authorization(sutProvider, orgId);

        sutProvider.GetDependency<IUpdateOrganizationReportV2Command>()
            .UpdateAsync(Arg.Any<UpdateOrganizationReportV2Request>())
            .Returns(expectedReport);

        sutProvider.GetDependency<IOrganizationReportStorageService>()
            .GetReportFileUploadUrlAsync(expectedReport, Arg.Any<ReportFile>())
            .Returns(uploadUrl);

        sutProvider.GetDependency<IOrganizationReportStorageService>()
            .FileUploadType
            .Returns(FileUploadType.Azure);

        // Act
        var result = await sutProvider.Sut.UpdateOrganizationReportAsync(orgId, reportId, request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<OrganizationReportFileResponseModel>(okResult.Value);
        Assert.Equal(uploadUrl, response.ReportFileUploadUrl);
        Assert.Equal(FileUploadType.Azure, response.FileUploadType);
        Assert.NotNull(response.ReportResponse);
    }

    [Theory, BitAutoData]
    public async Task UpdateOrganizationReportAsync_V2_WithoutAccess_ThrowsNotFoundException(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        Guid reportId,
        UpdateOrganizationReportV2RequestModel request)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.AccessIntelligenceVersion2)
            .Returns(true);

        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(false);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.UpdateOrganizationReportAsync(orgId, reportId, request));

        await sutProvider.GetDependency<IUpdateOrganizationReportV2Command>()
            .DidNotReceive()
            .UpdateAsync(Arg.Any<UpdateOrganizationReportV2Request>());
    }

    // SummaryData Field Endpoints

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
        var responseList = Assert.IsAssignableFrom<IEnumerable<OrganizationReportSummaryDataResponseModel>>(okResult.Value);
        Assert.Equal(expectedSummaryData.Count, responseList.Count());
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
        var response = Assert.IsType<OrganizationReportSummaryDataResponseModel>(okResult.Value);
        Assert.Equal(expectedSummaryData.SummaryData, response.EncryptedData);
        Assert.Equal(expectedSummaryData.ContentEncryptionKey, response.EncryptionKey);
        Assert.Equal(expectedSummaryData.RevisionDate, response.Date);
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
    public async Task UpdateOrganizationReportSummaryAsync_WithValidRequest_ReturnsOkResult(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        Guid reportId,
        UpdateOrganizationReportSummaryRequestModel request,
        OrganizationReport expectedReport)
    {
        // Arrange
        expectedReport.ReportFile = null;

        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(true);

        sutProvider.GetDependency<IUpdateOrganizationReportSummaryCommand>()
            .UpdateOrganizationReportSummaryAsync(Arg.Any<UpdateOrganizationReportSummaryRequest>())
            .Returns(expectedReport);

        // Act
        var result = await sutProvider.Sut.UpdateOrganizationReportSummaryAsync(orgId, reportId, request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var expectedResponse = new OrganizationReportResponseModel(expectedReport);
        Assert.Equivalent(expectedResponse, okResult.Value);
    }

    [Theory, BitAutoData]
    public async Task UpdateOrganizationReportSummaryAsync_WithoutAccess_ThrowsNotFoundException(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        Guid reportId,
        UpdateOrganizationReportSummaryRequestModel request)
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
    public async Task UpdateOrganizationReportSummaryAsync_CallsCorrectMethods(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        Guid reportId,
        UpdateOrganizationReportSummaryRequestModel request,
        OrganizationReport expectedReport)
    {
        // Arrange
        expectedReport.ReportFile = null;

        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(true);

        sutProvider.GetDependency<IUpdateOrganizationReportSummaryCommand>()
            .UpdateOrganizationReportSummaryAsync(Arg.Any<UpdateOrganizationReportSummaryRequest>())
            .Returns(expectedReport);

        // Act
        await sutProvider.Sut.UpdateOrganizationReportSummaryAsync(orgId, reportId, request);

        // Assert
        await sutProvider.GetDependency<ICurrentContext>()
            .Received(1)
            .AccessReports(orgId);

        await sutProvider.GetDependency<IUpdateOrganizationReportSummaryCommand>()
            .Received(1)
            .UpdateOrganizationReportSummaryAsync(Arg.Any<UpdateOrganizationReportSummaryRequest>());
    }

    // ReportData Field Endpoints

    [Theory, BitAutoData]
    public async Task GetOrganizationReportDataAsync_WithValidIds_ReturnsOkResult(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        Guid reportId,
        OrganizationReportDataResponse expectedReportData)
    {
        // Arrange
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
        var response = Assert.IsType<OrganizationReportDataResponseModel>(okResult.Value);
        Assert.Equal(expectedReportData.ReportData, response.ReportData);
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
        UpdateOrganizationReportDataRequestModel request,
        OrganizationReport expectedReport)
    {
        // Arrange
        expectedReport.ReportFile = null;

        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(true);

        sutProvider.GetDependency<IUpdateOrganizationReportDataCommand>()
            .UpdateOrganizationReportDataAsync(Arg.Any<UpdateOrganizationReportDataRequest>())
            .Returns(expectedReport);

        // Act
        var result = await sutProvider.Sut.UpdateOrganizationReportDataAsync(orgId, reportId, request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var expectedResponse = new OrganizationReportResponseModel(expectedReport);
        Assert.Equivalent(expectedResponse, okResult.Value);
    }

    [Theory, BitAutoData]
    public async Task UpdateOrganizationReportDataAsync_WithoutAccess_ThrowsNotFoundException(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        Guid reportId,
        UpdateOrganizationReportDataRequestModel request)
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
    public async Task UpdateOrganizationReportDataAsync_CallsCorrectMethods(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        Guid reportId,
        UpdateOrganizationReportDataRequestModel request,
        OrganizationReport expectedReport)
    {
        // Arrange
        expectedReport.ReportFile = null;

        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(true);

        sutProvider.GetDependency<IUpdateOrganizationReportDataCommand>()
            .UpdateOrganizationReportDataAsync(Arg.Any<UpdateOrganizationReportDataRequest>())
            .Returns(expectedReport);

        // Act
        await sutProvider.Sut.UpdateOrganizationReportDataAsync(orgId, reportId, request);

        // Assert
        await sutProvider.GetDependency<ICurrentContext>()
            .Received(1)
            .AccessReports(orgId);

        await sutProvider.GetDependency<IUpdateOrganizationReportDataCommand>()
            .Received(1)
            .UpdateOrganizationReportDataAsync(Arg.Any<UpdateOrganizationReportDataRequest>());
    }

    // ApplicationData Field Endpoints

    [Theory, BitAutoData]
    public async Task GetOrganizationReportApplicationDataAsync_WithValidIds_ReturnsOkResult(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        Guid reportId,
        OrganizationReportApplicationDataResponse expectedApplicationData)
    {
        // Arrange
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
        var response = Assert.IsType<OrganizationReportApplicationDataResponseModel>(okResult.Value);
        Assert.Equal(expectedApplicationData.ApplicationData, response.ApplicationData);
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
        Guid reportId,
        UpdateOrganizationReportApplicationDataRequestModel request,
        OrganizationReport expectedReport)
    {
        // Arrange
        expectedReport.ReportFile = null;

        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(true);

        sutProvider.GetDependency<IUpdateOrganizationReportApplicationDataCommand>()
            .UpdateOrganizationReportApplicationDataAsync(Arg.Any<UpdateOrganizationReportApplicationDataRequest>())
            .Returns(expectedReport);

        // Act
        var result = await sutProvider.Sut.UpdateOrganizationReportApplicationDataAsync(orgId, reportId, request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var expectedResponse = new OrganizationReportResponseModel(expectedReport);
        Assert.Equivalent(expectedResponse, okResult.Value);
    }

    [Theory, BitAutoData]
    public async Task UpdateOrganizationReportApplicationDataAsync_WithoutAccess_ThrowsNotFoundException(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        Guid reportId,
        UpdateOrganizationReportApplicationDataRequestModel request)
    {
        // Arrange
        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(false);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.UpdateOrganizationReportApplicationDataAsync(orgId, reportId, request));

        // Verify that the command was not called
        await sutProvider.GetDependency<IUpdateOrganizationReportApplicationDataCommand>()
            .DidNotReceive()
            .UpdateOrganizationReportApplicationDataAsync(Arg.Any<UpdateOrganizationReportApplicationDataRequest>());
    }

    [Theory, BitAutoData]
    public async Task UpdateOrganizationReportApplicationDataAsync_CallsCorrectMethods(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        Guid reportId,
        UpdateOrganizationReportApplicationDataRequestModel request,
        OrganizationReport expectedReport)
    {
        // Arrange
        expectedReport.ReportFile = null;

        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(true);

        sutProvider.GetDependency<IUpdateOrganizationReportApplicationDataCommand>()
            .UpdateOrganizationReportApplicationDataAsync(Arg.Any<UpdateOrganizationReportApplicationDataRequest>())
            .Returns(expectedReport);

        // Act
        await sutProvider.Sut.UpdateOrganizationReportApplicationDataAsync(orgId, reportId, request);

        // Assert
        await sutProvider.GetDependency<ICurrentContext>()
            .Received(1)
            .AccessReports(orgId);

        await sutProvider.GetDependency<IUpdateOrganizationReportApplicationDataCommand>()
            .Received(1)
            .UpdateOrganizationReportApplicationDataAsync(Arg.Any<UpdateOrganizationReportApplicationDataRequest>());
    }

    // Helper method for setting up V2 authorization mocks

    private static void SetupV2Authorization(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId)
    {
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.AccessIntelligenceVersion2)
            .Returns(true);

        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(true);

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(orgId)
            .Returns(new OrganizationAbility { UseRiskInsights = true });
    }
}
