using Bit.Api.AdminConsole.Authorization;
using Bit.Api.Dirt.Authorization;
using Bit.Api.Dirt.Models.Response;
using Bit.Api.Utilities;
using Bit.Core;
using Bit.Core.Context;
using Bit.Core.Dirt.Models.Data;
using Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;
using Bit.Core.Dirt.Reports.ReportFeatures.Requests;
using Bit.Core.Dirt.Reports.Services;
using Bit.Core.Exceptions;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Dirt.Controllers;

[Route("reports/v2/organizations")]
[Authorize("Application")]
[Authorize<UseRiskInsightsRequirement>]
public class OrganizationReportsV2Controller : Controller
{
    private readonly ICurrentContext _currentContext;
    private readonly IOrganizationReportStorageService _storageService;
    private readonly ICreateOrganizationReportFileStorageCommand _createCommand;
    private readonly IUpdateOrganizationReportDataFileStorageCommand _updateDataCommand;
    private readonly IUpdateOrganizationReportSummaryFileStorageCommand _updateSummaryCommand;
    private readonly IUpdateOrganizationReportApplicationDataFileStorageCommand _updateApplicationCommand;
    private readonly IGetOrganizationReportQuery _getOrganizationReportQuery;
    private readonly IGetOrganizationReportDataFileStorageQuery _getDataQuery;
    private readonly IGetOrganizationReportSummaryDataFileStorageQuery _getSummaryQuery;
    private readonly IGetOrganizationReportApplicationDataFileStorageQuery _getApplicationQuery;
    private readonly IGetOrganizationReportSummaryDataByDateRangeQuery _getSummaryByDateRangeQuery;

    public OrganizationReportsV2Controller(
        ICurrentContext currentContext,
        IOrganizationReportStorageService storageService,
        ICreateOrganizationReportFileStorageCommand createCommand,
        IUpdateOrganizationReportDataFileStorageCommand updateDataCommand,
        IUpdateOrganizationReportSummaryFileStorageCommand updateSummaryCommand,
        IUpdateOrganizationReportApplicationDataFileStorageCommand updateApplicationCommand,
        IGetOrganizationReportQuery getOrganizationReportQuery,
        IGetOrganizationReportDataFileStorageQuery getDataQuery,
        IGetOrganizationReportSummaryDataFileStorageQuery getSummaryQuery,
        IGetOrganizationReportApplicationDataFileStorageQuery getApplicationQuery,
        IGetOrganizationReportSummaryDataByDateRangeQuery getSummaryByDateRangeQuery)
    {
        _currentContext = currentContext;
        _storageService = storageService;
        _createCommand = createCommand;
        _updateDataCommand = updateDataCommand;
        _updateSummaryCommand = updateSummaryCommand;
        _updateApplicationCommand = updateApplicationCommand;
        _getOrganizationReportQuery = getOrganizationReportQuery;
        _getDataQuery = getDataQuery;
        _getSummaryQuery = getSummaryQuery;
        _getApplicationQuery = getApplicationQuery;
        _getSummaryByDateRangeQuery = getSummaryByDateRangeQuery;
    }

    #region Write Endpoints - Return Upload URLs

    [HttpPost("{organizationId}")]
    public async Task<OrganizationReportFileUploadResponseModel> CreateOrganizationReportAsync(
        Guid organizationId,
        [FromBody] AddOrganizationReportRequest request)
    {
        if (request.OrganizationId != organizationId)
        {
            throw new BadRequestException("Organization ID in the request body must match the route parameter");
        }

        var (report, reportFileId) = await _createCommand.CreateAsync(request);

        return new OrganizationReportFileUploadResponseModel
        {
            FileUploadType = _storageService.FileUploadType,
            ReportDataUploadUrl = await _storageService.GetReportDataUploadUrlAsync(report, reportFileId),
            SummaryDataUploadUrl = await _storageService.GetSummaryDataUploadUrlAsync(report, reportFileId),
            ApplicationDataUploadUrl = await _storageService.GetApplicationDataUploadUrlAsync(report, reportFileId),
            ReportFileId = reportFileId,
            ReportResponse = new OrganizationReportResponseModel(report)
        };
    }

    [HttpPatch("{organizationId}/data/report/{reportId}")]
    public async Task<OrganizationReportFileUploadResponseModel> GetReportDataUploadUrlAsync(
        Guid organizationId,
        Guid reportId,
        [FromBody] UpdateOrganizationReportDataRequest request,
        [FromQuery] string reportFileId)
    {
        if (request.OrganizationId != organizationId || request.ReportId != reportId)
        {
            throw new BadRequestException("Organization ID and Report ID must match route parameters");
        }

        if (string.IsNullOrEmpty(reportFileId))
        {
            throw new BadRequestException("ReportFileId query parameter is required");
        }

        var uploadUrl = await _updateDataCommand.GetUploadUrlAsync(request, reportFileId);

        var report = await _getOrganizationReportQuery.GetOrganizationReportAsync(reportId);

        return new OrganizationReportFileUploadResponseModel
        {
            FileUploadType = _storageService.FileUploadType,
            ReportDataUploadUrl = uploadUrl,
            ReportFileId = reportFileId,
            ReportResponse = new OrganizationReportResponseModel(report)
        };
    }

    [HttpPatch("{organizationId}/data/summary/{reportId}")]
    public async Task<OrganizationReportFileUploadResponseModel> GetSummaryDataUploadUrlAsync(
        Guid organizationId,
        Guid reportId,
        [FromBody] UpdateOrganizationReportSummaryRequest request,
        [FromQuery] string reportFileId)
    {
        if (request.OrganizationId != organizationId || request.ReportId != reportId)
        {
            throw new BadRequestException("Organization ID and Report ID must match route parameters");
        }

        if (string.IsNullOrEmpty(reportFileId))
        {
            throw new BadRequestException("ReportFileId query parameter is required");
        }

        var uploadUrl = await _updateSummaryCommand.GetUploadUrlAsync(request, reportFileId);

        var report = await _getOrganizationReportQuery.GetOrganizationReportAsync(reportId);

        return new OrganizationReportFileUploadResponseModel
        {
            FileUploadType = _storageService.FileUploadType,
            SummaryDataUploadUrl = uploadUrl,
            ReportFileId = reportFileId,
            ReportResponse = new OrganizationReportResponseModel(report)
        };
    }

    [HttpPatch("{organizationId}/data/application/{reportId}")]
    public async Task<OrganizationReportFileUploadResponseModel> GetApplicationDataUploadUrlAsync(
        Guid organizationId,
        Guid reportId,
        [FromBody] UpdateOrganizationReportApplicationDataRequest request,
        [FromQuery] string reportFileId)
    {
        if (request.OrganizationId != organizationId || request.Id != reportId)
        {
            throw new BadRequestException("Organization ID and Report ID must match route parameters");
        }

        if (string.IsNullOrEmpty(reportFileId))
        {
            throw new BadRequestException("ReportFileId query parameter is required");
        }

        var uploadUrl = await _updateApplicationCommand.GetUploadUrlAsync(request, reportFileId);

        var report = await _getOrganizationReportQuery.GetOrganizationReportAsync(reportId);

        return new OrganizationReportFileUploadResponseModel
        {
            FileUploadType = _storageService.FileUploadType,
            ApplicationDataUploadUrl = uploadUrl,
            ReportFileId = reportFileId,
            ReportResponse = new OrganizationReportResponseModel(report)
        };
    }

    #endregion

    #region Self-Hosted Direct Upload Endpoints

    [HttpPost("{organizationId}/{reportId}/file/report-data")]
    [SelfHosted(SelfHostedOnly = true)]
    [RequestSizeLimit(Constants.FileSize501mb)]
    [DisableFormValueModelBinding]
    public async Task UploadReportDataAsync(Guid organizationId, Guid reportId, [FromQuery] string reportFileId)
    {
        if (!Request?.ContentType?.Contains("multipart/") ?? true)
        {
            throw new BadRequestException("Invalid content.");
        }

        if (string.IsNullOrEmpty(reportFileId))
        {
            throw new BadRequestException("ReportFileId query parameter is required");
        }

        var report = await _getOrganizationReportQuery.GetOrganizationReportAsync(reportId);
        if (report.OrganizationId != organizationId)
        {
            throw new BadRequestException("Invalid report ID");
        }

        await Request.GetFileAsync(async (stream) =>
        {
            await _storageService.UploadReportDataAsync(report, reportFileId, stream);
        });
    }

    [HttpPost("{organizationId}/{reportId}/file/summary-data")]
    [SelfHosted(SelfHostedOnly = true)]
    [RequestSizeLimit(Constants.FileSize501mb)]
    [DisableFormValueModelBinding]
    public async Task UploadSummaryDataAsync(Guid organizationId, Guid reportId, [FromQuery] string reportFileId)
    {
        if (!Request?.ContentType?.Contains("multipart/") ?? true)
        {
            throw new BadRequestException("Invalid content.");
        }

        if (string.IsNullOrEmpty(reportFileId))
        {
            throw new BadRequestException("ReportFileId query parameter is required");
        }

        var report = await _getOrganizationReportQuery.GetOrganizationReportAsync(reportId);
        if (report.OrganizationId != organizationId)
        {
            throw new BadRequestException("Invalid report ID");
        }

        await Request.GetFileAsync(async (stream) =>
        {
            await _storageService.UploadSummaryDataAsync(report, reportFileId, stream);
        });
    }

    [HttpPost("{organizationId}/{reportId}/file/application-data")]
    [SelfHosted(SelfHostedOnly = true)]
    [RequestSizeLimit(Constants.FileSize501mb)]
    [DisableFormValueModelBinding]
    public async Task UploadApplicationDataAsync(Guid organizationId, Guid reportId, [FromQuery] string reportFileId)
    {
        if (!Request?.ContentType?.Contains("multipart/") ?? true)
        {
            throw new BadRequestException("Invalid content.");
        }

        if (string.IsNullOrEmpty(reportFileId))
        {
            throw new BadRequestException("ReportFileId query parameter is required");
        }

        var report = await _getOrganizationReportQuery.GetOrganizationReportAsync(reportId);
        if (report.OrganizationId != organizationId)
        {
            throw new BadRequestException("Invalid report ID");
        }

        await Request.GetFileAsync(async (stream) =>
        {
            await _storageService.UploadApplicationDataAsync(report, reportFileId, stream);
        });
    }

    #endregion

    #region Read Endpoints - Return Download URLs for File Storage

    [HttpGet("{organizationId}/{reportId}")]
    public async Task<OrganizationReportResponseModel> GetOrganizationReportAsync(
        Guid organizationId,
        Guid reportId)
    {
        var report = await _getOrganizationReportQuery.GetOrganizationReportAsync(reportId);

        if (report.OrganizationId != organizationId)
        {
            throw new BadRequestException("Invalid report ID");
        }

        return new OrganizationReportResponseModel(report);
    }

    [HttpGet("{organizationId}/latest")]
    public async Task<OrganizationReportResponseModel?> GetLatestOrganizationReportAsync(Guid organizationId)
    {
        var latestReport = await _getOrganizationReportQuery.GetLatestOrganizationReportAsync(organizationId);
        return latestReport == null ? null : new OrganizationReportResponseModel(latestReport);
    }

    [HttpGet("{organizationId}/data/report/{reportId}/file/{reportFileId}")]
    public async Task<OrganizationReportDataFileStorageResponse> GetOrganizationReportDataAsync(
        Guid organizationId,
        Guid reportId,
        string reportFileId)
    {
        return await _getDataQuery.GetOrganizationReportDataAsync(organizationId, reportId, reportFileId);
    }

    [HttpGet("{organizationId}/data/summary/{reportId}/file/{reportFileId}")]
    public async Task<OrganizationReportSummaryDataFileStorageResponse> GetOrganizationReportSummaryAsync(
        Guid organizationId,
        Guid reportId,
        string reportFileId)
    {
        return await _getSummaryQuery.GetOrganizationReportSummaryDataAsync(organizationId, reportId, reportFileId);
    }

    [HttpGet("{organizationId}/data/application/{reportId}/file/{reportFileId}")]
    public async Task<OrganizationReportApplicationDataFileStorageResponse> GetOrganizationReportApplicationDataAsync(
        Guid organizationId,
        Guid reportId,
        string reportFileId)
    {
        return await _getApplicationQuery.GetOrganizationReportApplicationDataAsync(organizationId, reportId, reportFileId);
    }

    [HttpGet("{organizationId}/data/summary")]
    public async Task<IEnumerable<OrganizationReportSummaryDataResponse>> GetOrganizationReportSummaryDataByDateRangeAsync(
        Guid organizationId,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate)
    {
        if (organizationId == Guid.Empty)
        {
            throw new BadRequestException("Organization ID is required.");
        }

        return await _getSummaryByDateRangeQuery.GetOrganizationReportSummaryDataByDateRangeAsync(
            organizationId, startDate, endDate);
    }

    #endregion
}
