using Bit.Api.Dirt.Models.Response;
using Bit.Api.Utilities;
using Bit.Core;
using Bit.Core.Context;
using Bit.Core.Dirt.Models.Data;
using Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;
using Bit.Core.Dirt.Reports.ReportFeatures.Requests;
using Bit.Core.Dirt.Reports.Services;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Dirt.Controllers;

[Route("reports/v2/organizations")]
[Authorize("Application")]
public class OrganizationReportsV2Controller : Controller
{
    private readonly ICurrentContext _currentContext;
    private readonly IApplicationCacheService _applicationCacheService;
    private readonly IOrganizationReportStorageService _storageService;
    private readonly ICreateOrganizationReportStorageCommand _createCommand;
    private readonly IUpdateOrganizationReportDataFileStorageCommand _updateDataCommand;
    private readonly IGetOrganizationReportQuery _getOrganizationReportQuery;
    private readonly IGetOrganizationReportDataFileStorageQuery _getDataQuery;
    private readonly IGetOrganizationReportApplicationDataV2Query _getApplicationDataQuery;
    private readonly IUpdateOrganizationReportApplicationDataV2Command _updateApplicationDataCommand;
    private readonly IUpdateOrganizationReportCommand _updateOrganizationReportCommand;

    public OrganizationReportsV2Controller(
        ICurrentContext currentContext,
        IApplicationCacheService applicationCacheService,
        IOrganizationReportStorageService storageService,
        ICreateOrganizationReportStorageCommand createCommand,
        IUpdateOrganizationReportDataFileStorageCommand updateDataCommand,
        IGetOrganizationReportQuery getOrganizationReportQuery,
        IGetOrganizationReportDataFileStorageQuery getDataQuery,
        IGetOrganizationReportApplicationDataV2Query getApplicationDataQuery,
        IUpdateOrganizationReportApplicationDataV2Command updateApplicationDataCommand,
        IUpdateOrganizationReportCommand updateOrganizationReportCommand)
    {
        _currentContext = currentContext;
        _applicationCacheService = applicationCacheService;
        _storageService = storageService;
        _createCommand = createCommand;
        _updateDataCommand = updateDataCommand;
        _getOrganizationReportQuery = getOrganizationReportQuery;
        _getDataQuery = getDataQuery;
        _getApplicationDataQuery = getApplicationDataQuery;
        _updateApplicationDataCommand = updateApplicationDataCommand;
        _updateOrganizationReportCommand = updateOrganizationReportCommand;
    }

    private async Task AuthorizeAsync(Guid organizationId)
    {
        if (!await _currentContext.AccessReports(organizationId))
        {
            throw new NotFoundException();
        }

        var orgAbility = await _applicationCacheService.GetOrganizationAbilityAsync(organizationId);
        if (orgAbility is null || !orgAbility.UseRiskInsights)
        {
            throw new BadRequestException("Your organization's plan does not support this feature.");
        }
    }

    #region Whole Report Endpoints

    [HttpPost("{organizationId}")]
    public async Task<OrganizationReportV2ResponseModel> CreateOrganizationReportAsync(
        Guid organizationId,
        [FromBody] AddOrganizationReportRequest request)
    {
        if (organizationId == Guid.Empty)
        {
            throw new BadRequestException("Organization ID is required.");
        }

        if (request.OrganizationId != organizationId)
        {
            throw new BadRequestException("Organization ID in the request body must match the route parameter");
        }

        await AuthorizeAsync(organizationId);

        var report = await _createCommand.CreateAsync(request);

        return new OrganizationReportV2ResponseModel
        {
            ReportDataUploadUrl = await _storageService.GetReportDataUploadUrlAsync(report, report.FileId!),
            ReportResponse = new OrganizationReportResponseModel(report)
        };
    }

    [HttpGet("{organizationId}/{reportId}")]
    public async Task<OrganizationReportResponseModel> GetOrganizationReportAsync(
        Guid organizationId,
        Guid reportId)
    {
        await AuthorizeAsync(organizationId);

        var report = await _getOrganizationReportQuery.GetOrganizationReportAsync(reportId);

        if (report.OrganizationId != organizationId)
        {
            throw new BadRequestException("Invalid report ID");
        }

        return new OrganizationReportResponseModel(report);
    }



    [HttpPatch("{organizationId}/data/report/{reportId}")]
    public async Task<OrganizationReportV2ResponseModel> GetReportDataUploadUrlAsync(
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

        await AuthorizeAsync(organizationId);

        var uploadUrl = await _updateDataCommand.GetUploadUrlAsync(request, reportFileId);

        var report = await _getOrganizationReportQuery.GetOrganizationReportAsync(reportId);

        return new OrganizationReportV2ResponseModel
        {
            ReportDataUploadUrl = uploadUrl,
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
        await AuthorizeAsync(organizationId);

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

    #endregion

    #region Whole Report Endpoints





    #endregion

    #region ApplicationData Field Endpoints

    [HttpGet("{organizationId}/data/application/{reportId}")]
    public async Task<OrganizationReportApplicationDataResponse> GetOrganizationReportApplicationDataV2Async(
        Guid organizationId, Guid reportId)
    {
        if (organizationId == Guid.Empty) throw new BadRequestException("OrganizationId is required.");

        if (reportId == Guid.Empty) throw new BadRequestException("ReportId is required.");

        await AuthorizeAsync(organizationId);

        var applicationData = await _getApplicationDataQuery
            .GetApplicationDataAsync(organizationId, reportId);

        if (applicationData == null) throw new NotFoundException("Organization report application data not found.");

        return applicationData;
    }

    [HttpPatch("{organizationId}/data/application/{reportId}")]
    public async Task<OrganizationReportResponseModel> UpdateOrganizationReportApplicationDataV2Async(
        Guid organizationId, Guid reportId,
        [FromBody] UpdateOrganizationReportApplicationDataRequest request)
    {
        if (request.OrganizationId != organizationId) throw new BadRequestException("Organization ID in the request body must match the route parameter");

        if (request.Id != reportId) throw new BadRequestException("Report ID in the request body must match the route parameter");

        if (string.IsNullOrWhiteSpace(request.ApplicationData)) throw new BadRequestException("Application Data is required");

        await AuthorizeAsync(organizationId);

        var updatedReport = await _updateApplicationDataCommand
            .UpdateApplicationDataAsync(request);

        return new OrganizationReportResponseModel(updatedReport);
    }

    #endregion
}
