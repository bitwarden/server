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
using Bit.Core.Dirt.Repositories;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace Bit.Api.Test.Dirt;

[ControllerCustomize(typeof(OrganizationReportsController))]
[SutProviderCustomize]
public class OrganizationReportControllerTests
{
    // GetLatestOrganizationReportAsync

    [Theory, BitAutoData]
    public async Task GetLatestOrganizationReportAsync_WithValidatedFile_ReturnsOkWithDownloadUrl(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        OrganizationReport expectedReport,
        string downloadUrl)
    {
        // Arrange
        var reportFile = new ReportFile { Id = "file-id", FileName = "report.json", Size = 1024, Validated = true };
        expectedReport.SetReportFile(reportFile);

        SetupAuthorization(sutProvider, orgId);

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.AccessIntelligenceVersion2)
            .Returns(true);

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
    public async Task GetLatestOrganizationReportAsync_WithNoFile_ReturnsOkWithNullDownloadUrl(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        OrganizationReport expectedReport)
    {
        // Arrange
        expectedReport.ReportFile = null;

        SetupAuthorization(sutProvider, orgId);

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
    public async Task GetLatestOrganizationReportAsync_WithoutAccess_ThrowsNotFoundException(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId)
    {
        // Arrange
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
    public async Task GetLatestOrganizationReportAsync_NoUseRiskInsights_ThrowsBadRequestException(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId)
    {
        // Arrange
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

        SetupAuthorization(sutProvider, orgId);

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.AccessIntelligenceVersion2)
            .Returns(false);

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

        SetupV2Authorization(sutProvider, orgId);

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

    // GetOrganizationReportAsync

    [Theory, BitAutoData]
    public async Task GetOrganizationReportAsync_WithValidatedFile_ReturnsOkWithDownloadUrl(
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

        SetupAuthorization(sutProvider, orgId);

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
    public async Task GetOrganizationReportAsync_WithNoFile_ReturnsOkWithoutDownloadUrl(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        Guid reportId,
        OrganizationReport expectedReport)
    {
        // Arrange
        expectedReport.OrganizationId = orgId;
        expectedReport.ReportFile = null;

        SetupAuthorization(sutProvider, orgId);

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
    public async Task GetOrganizationReportAsync_WithOrgMismatch_ThrowsBadRequestException(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        Guid reportId,
        OrganizationReport expectedReport)
    {
        // Arrange
        expectedReport.OrganizationId = Guid.NewGuid();

        SetupAuthorization(sutProvider, orgId);

        sutProvider.GetDependency<IGetOrganizationReportQuery>()
            .GetOrganizationReportAsync(reportId)
            .Returns(expectedReport);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.GetOrganizationReportAsync(orgId, reportId));

        Assert.Equal("Invalid report ID", exception.Message);
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
            .Returns(false);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetOrganizationReportAsync(orgId, reportId));

        await sutProvider.GetDependency<IGetOrganizationReportQuery>()
            .DidNotReceive()
            .GetOrganizationReportAsync(Arg.Any<Guid>());
    }

    // DeleteOrganizationReportAsync

    [Theory, BitAutoData]
    public async Task DeleteOrganizationReportAsync_WithFile_DeletesDbThenStorage(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        OrganizationReport report)
    {
        // Arrange
        var reportFile = new ReportFile { Id = "file-id", FileName = "report.json", Size = 1024, Validated = true };
        report.OrganizationId = orgId;
        report.SetReportFile(reportFile);

        SetupAuthorization(sutProvider, orgId);

        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetByIdAsync(report.Id)
            .Returns(report);

        // Act
        await sutProvider.Sut.DeleteOrganizationReportAsync(orgId, report.Id);

        // Assert
        await sutProvider.GetDependency<IOrganizationReportRepository>()
            .Received(1)
            .DeleteAsync(report);

        await sutProvider.GetDependency<IOrganizationReportStorageService>()
            .Received(1)
            .DeleteReportFilesAsync(report, "file-id");

        await sutProvider.GetDependency<IFusionCache>()
            .Received(1)
            .RemoveByTagAsync(
                OrganizationReportCacheConstants.BuildCacheTagForOrganizationReports(orgId));
    }

    [Theory, BitAutoData]
    public async Task DeleteOrganizationReportAsync_WithNoFile_DeletesDbOnly(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        OrganizationReport report)
    {
        // Arrange
        report.OrganizationId = orgId;
        report.ReportFile = null;

        SetupAuthorization(sutProvider, orgId);

        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetByIdAsync(report.Id)
            .Returns(report);

        // Act
        await sutProvider.Sut.DeleteOrganizationReportAsync(orgId, report.Id);

        // Assert
        await sutProvider.GetDependency<IOrganizationReportRepository>()
            .Received(1)
            .DeleteAsync(report);

        await sutProvider.GetDependency<IOrganizationReportStorageService>()
            .DidNotReceive()
            .DeleteReportFilesAsync(Arg.Any<OrganizationReport>(), Arg.Any<string>());

        await sutProvider.GetDependency<IFusionCache>()
            .Received(1)
            .RemoveByTagAsync(
                OrganizationReportCacheConstants.BuildCacheTagForOrganizationReports(orgId));
    }

    [Theory, BitAutoData]
    public async Task DeleteOrganizationReportAsync_ReportNotFound_ThrowsNotFoundException(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        Guid reportId)
    {
        // Arrange
        SetupAuthorization(sutProvider, orgId);

        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetByIdAsync(reportId)
            .Returns((OrganizationReport)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.DeleteOrganizationReportAsync(orgId, reportId));

        await sutProvider.GetDependency<IOrganizationReportRepository>()
            .DidNotReceive()
            .DeleteAsync(Arg.Any<OrganizationReport>());
    }

    [Theory, BitAutoData]
    public async Task DeleteOrganizationReportAsync_OrgMismatch_ThrowsBadRequestException(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        OrganizationReport report)
    {
        // Arrange
        report.OrganizationId = Guid.NewGuid();

        SetupAuthorization(sutProvider, orgId);

        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetByIdAsync(report.Id)
            .Returns(report);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.DeleteOrganizationReportAsync(orgId, report.Id));

        Assert.Equal("Invalid report ID", exception.Message);

        await sutProvider.GetDependency<IOrganizationReportRepository>()
            .DidNotReceive()
            .DeleteAsync(Arg.Any<OrganizationReport>());
    }

    [Theory, BitAutoData]
    public async Task DeleteOrganizationReportAsync_WithoutAccess_ThrowsNotFoundException(
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
            sutProvider.Sut.DeleteOrganizationReportAsync(orgId, reportId));

        await sutProvider.GetDependency<IOrganizationReportRepository>()
            .DidNotReceive()
            .DeleteAsync(Arg.Any<OrganizationReport>());
    }

    // RenewFileUploadUrlAsync

    [Theory, BitAutoData]
    public async Task RenewFileUploadUrlAsync_WithUnvalidatedFile_ReturnsRenewedUrl(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        OrganizationReport report,
        string uploadUrl)
    {
        // Arrange
        var reportFile = new ReportFile { Id = "file-id", FileName = "report.json", Size = 1024, Validated = false };
        report.OrganizationId = orgId;
        report.SetReportFile(reportFile);

        SetupV2Authorization(sutProvider, orgId);

        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetByIdAsync(report.Id)
            .Returns(report);

        sutProvider.GetDependency<IOrganizationReportStorageService>()
            .GetReportFileUploadUrlAsync(report, Arg.Any<ReportFile>())
            .Returns(uploadUrl);

        sutProvider.GetDependency<IOrganizationReportStorageService>()
            .FileUploadType
            .Returns(FileUploadType.Azure);

        // Act
        var result = await sutProvider.Sut.RenewFileUploadUrlAsync(orgId, report.Id, "file-id");

        // Assert
        Assert.Equal(uploadUrl, result.ReportFileUploadUrl);
        Assert.Equal(FileUploadType.Azure, result.FileUploadType);
        Assert.NotNull(result.ReportResponse);
    }

    [Theory, BitAutoData]
    public async Task RenewFileUploadUrlAsync_ReportNotFound_ThrowsNotFoundException(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        Guid reportId)
    {
        // Arrange
        SetupV2Authorization(sutProvider, orgId);

        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetByIdAsync(reportId)
            .Returns((OrganizationReport)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.RenewFileUploadUrlAsync(orgId, reportId, "file-id"));
    }

    [Theory, BitAutoData]
    public async Task RenewFileUploadUrlAsync_OrgMismatch_ThrowsBadRequestException(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        OrganizationReport report)
    {
        // Arrange
        report.OrganizationId = Guid.NewGuid();

        SetupV2Authorization(sutProvider, orgId);

        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetByIdAsync(report.Id)
            .Returns(report);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.RenewFileUploadUrlAsync(orgId, report.Id, "file-id"));

        Assert.Equal("Invalid report ID", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task RenewFileUploadUrlAsync_FileAlreadyValidated_ThrowsNotFoundException(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        OrganizationReport report)
    {
        // Arrange
        var reportFile = new ReportFile { Id = "file-id", FileName = "report.json", Size = 1024, Validated = true };
        report.OrganizationId = orgId;
        report.SetReportFile(reportFile);

        SetupV2Authorization(sutProvider, orgId);

        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetByIdAsync(report.Id)
            .Returns(report);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.RenewFileUploadUrlAsync(orgId, report.Id, "file-id"));
    }

    [Theory, BitAutoData]
    public async Task RenewFileUploadUrlAsync_NoFileData_ThrowsNotFoundException(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        OrganizationReport report)
    {
        // Arrange
        report.OrganizationId = orgId;
        report.ReportFile = null;

        SetupV2Authorization(sutProvider, orgId);

        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetByIdAsync(report.Id)
            .Returns(report);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.RenewFileUploadUrlAsync(orgId, report.Id, "file-id"));
    }

    [Theory, BitAutoData]
    public async Task RenewFileUploadUrlAsync_MismatchedFileId_ThrowsNotFoundException(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        OrganizationReport report)
    {
        // Arrange
        var reportFile = new ReportFile { Id = "file-id", FileName = "report.json", Size = 1024, Validated = false };
        report.OrganizationId = orgId;
        report.SetReportFile(reportFile);

        SetupV2Authorization(sutProvider, orgId);

        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetByIdAsync(report.Id)
            .Returns(report);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.RenewFileUploadUrlAsync(orgId, report.Id, "wrong-file-id"));
    }

    [Theory, BitAutoData]
    public async Task RenewFileUploadUrlAsync_NullReportFileId_ThrowsBadRequestException(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        Guid reportId)
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.RenewFileUploadUrlAsync(orgId, reportId, null));

        Assert.Equal("ReportFileId is required.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task RenewFileUploadUrlAsync_EmptyReportFileId_ThrowsBadRequestException(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        Guid reportId)
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.RenewFileUploadUrlAsync(orgId, reportId, string.Empty));

        Assert.Equal("ReportFileId is required.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task DeleteOrganizationReportAsync_StorageFailure_StillCompletesWithoutThrowing(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        OrganizationReport report)
    {
        // Arrange
        var reportFile = new ReportFile { Id = "file-id", FileName = "report.json", Size = 1024, Validated = true };
        report.OrganizationId = orgId;
        report.SetReportFile(reportFile);

        SetupAuthorization(sutProvider, orgId);

        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetByIdAsync(report.Id)
            .Returns(report);

        sutProvider.GetDependency<IOrganizationReportStorageService>()
            .DeleteReportFilesAsync(report, "file-id")
            .ThrowsAsync(new Exception("Azure storage unavailable"));

        // Act — should not throw despite storage failure
        await sutProvider.Sut.DeleteOrganizationReportAsync(orgId, report.Id);

        // Assert — DB delete and cache invalidation still happened
        await sutProvider.GetDependency<IOrganizationReportRepository>()
            .Received(1)
            .DeleteAsync(report);

        await sutProvider.GetDependency<IFusionCache>()
            .Received(1)
            .RemoveByTagAsync(
                OrganizationReportCacheConstants.BuildCacheTagForOrganizationReports(orgId));

        sutProvider.GetDependency<ILogger<OrganizationReportsController>>()
            .Received(1)
            .Log(
                LogLevel.Warning,
                Arg.Any<EventId>(),
                Arg.Any<object>(),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception?, string>>());
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

        SetupAuthorization(sutProvider, orgId);

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.AccessIntelligenceVersion2)
            .Returns(false);

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
    public async Task UpdateOrganizationReportAsync_V2_ReturnsReportResponseModel(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        Guid reportId,
        UpdateOrganizationReportV2RequestModel request,
        OrganizationReport expectedReport)
    {
        // Arrange
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
        SetupAuthorization(sutProvider, orgId);

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
        SetupAuthorization(sutProvider, orgId);

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
        SetupAuthorization(sutProvider, orgId);

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

        SetupAuthorization(sutProvider, orgId);

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

        SetupAuthorization(sutProvider, orgId);

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

    // ApplicationData Field Endpoints

    [Theory, BitAutoData]
    public async Task GetOrganizationReportApplicationDataAsync_WithValidIds_ReturnsOkResult(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId,
        Guid reportId,
        OrganizationReportApplicationDataResponse expectedApplicationData)
    {
        // Arrange
        SetupAuthorization(sutProvider, orgId);

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
        SetupAuthorization(sutProvider, orgId);

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
        SetupAuthorization(sutProvider, orgId);

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

        SetupAuthorization(sutProvider, orgId);

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

        SetupAuthorization(sutProvider, orgId);

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

    // Helper methods for authorization mocks

    private static void SetupAuthorization(
        SutProvider<OrganizationReportsController> sutProvider,
        Guid orgId)
    {
        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(orgId)
            .Returns(true);

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(orgId)
            .Returns(new OrganizationAbility { UseRiskInsights = true });
    }

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
