using Bit.Api.Dirt.Models.Response;
using Bit.Api.Utilities;
using Bit.Core;
using Bit.Core.Context;
using Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;
using Bit.Core.Dirt.Reports.ReportFeatures.Requests;
using Bit.Core.Dirt.Reports.Services;
using Bit.Core.Dirt.Repositories;
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
    private readonly ICreateOrganizationReportV2Command _createCommand;
    private readonly IUpdateOrganizationReportDataV2Command _updateDataCommand;
    private readonly IGetOrganizationReportQuery _getOrganizationReportQuery;
    private readonly IGetOrganizationReportDataV2Query _getDataQuery;
    private readonly IUpdateOrganizationReportCommand _updateOrganizationReportCommand;
    private readonly IOrganizationReportRepository _organizationReportRepo;

    public OrganizationReportsV2Controller(
        ICurrentContext currentContext,
        IApplicationCacheService applicationCacheService,
        IOrganizationReportStorageService storageService,
        ICreateOrganizationReportV2Command createCommand,
        IUpdateOrganizationReportDataV2Command updateDataCommand,
        IGetOrganizationReportQuery getOrganizationReportQuery,
        IGetOrganizationReportDataV2Query getDataQuery,
        IUpdateOrganizationReportCommand updateOrganizationReportCommand,
        IOrganizationReportRepository organizationReportRepo)
    {
        _currentContext = currentContext;
        _applicationCacheService = applicationCacheService;
        _storageService = storageService;
        _createCommand = createCommand;
        _updateDataCommand = updateDataCommand;
        _getOrganizationReportQuery = getOrganizationReportQuery;
        _getDataQuery = getDataQuery;
        _updateOrganizationReportCommand = updateOrganizationReportCommand;
        _organizationReportRepo = organizationReportRepo;
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

        var fileData = report.GetReportFileData()!;

        return new OrganizationReportV2ResponseModel
        {
            ReportDataUploadUrl = await _storageService.GetReportDataUploadUrlAsync(report, fileData),
            ReportResponse = new OrganizationReportResponseModel(report),
            FileUploadType = _storageService.FileUploadType
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
            ReportResponse = new OrganizationReportResponseModel(report),
            FileUploadType = _storageService.FileUploadType
        };
    }

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

        var fileData = report.GetReportFileData();
        if (fileData == null || fileData.Id != reportFileId)
        {
            throw new NotFoundException();
        }

        await Request.GetFileAsync(async (stream) =>
        {
            await _storageService.UploadReportDataAsync(report, fileData, stream);
        });

        var (valid, length) = await _storageService.ValidateFileAsync(report, fileData, 0, Constants.FileSize501mb);
        if (!valid)
        {
            throw new BadRequestException("File received does not match expected constraints.");
        }

        fileData.Validated = true;
        fileData.Size = length;
        report.SetReportFileData(fileData);
        report.RevisionDate = DateTime.UtcNow;
        await _organizationReportRepo.ReplaceAsync(report);
    }
}
