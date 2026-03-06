using System.Text.Json;
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

[Route("reports/organizations")]
[Authorize("Application")]
public class OrganizationReportsController : Controller
{
    private readonly ICurrentContext _currentContext;
    private readonly IGetOrganizationReportQuery _getOrganizationReportQuery;
    private readonly IAddOrganizationReportCommand _addOrganizationReportCommand;
    private readonly IUpdateOrganizationReportCommand _updateOrganizationReportCommand;
    private readonly IUpdateOrganizationReportSummaryCommand _updateOrganizationReportSummaryCommand;
    private readonly IGetOrganizationReportSummaryDataQuery _getOrganizationReportSummaryDataQuery;
    private readonly IGetOrganizationReportSummaryDataByDateRangeQuery _getOrganizationReportSummaryDataByDateRangeQuery;
    private readonly IGetOrganizationReportDataQuery _getOrganizationReportDataQuery;
    private readonly IUpdateOrganizationReportDataCommand _updateOrganizationReportDataCommand;
    private readonly IGetOrganizationReportApplicationDataQuery _getOrganizationReportApplicationDataQuery;
    private readonly IUpdateOrganizationReportApplicationDataCommand _updateOrganizationReportApplicationDataCommand;
    private readonly IFeatureService _featureService;
    private readonly IApplicationCacheService _applicationCacheService;
    private readonly IOrganizationReportStorageService _storageService;
    private readonly ICreateOrganizationReportCommand _createReportCommand;
    private readonly IOrganizationReportRepository _organizationReportRepo;
    private readonly IUpdateOrganizationReportV2Command _updateReportV2Command;
    private readonly IValidateOrganizationReportFileCommand _validateCommand;
    private readonly ILogger<OrganizationReportsController> _logger;

    public OrganizationReportsController(
        ICurrentContext currentContext,
        IGetOrganizationReportQuery getOrganizationReportQuery,
        IAddOrganizationReportCommand addOrganizationReportCommand,
        IUpdateOrganizationReportCommand updateOrganizationReportCommand,
        IUpdateOrganizationReportSummaryCommand updateOrganizationReportSummaryCommand,
        IGetOrganizationReportSummaryDataQuery getOrganizationReportSummaryDataQuery,
        IGetOrganizationReportSummaryDataByDateRangeQuery getOrganizationReportSummaryDataByDateRangeQuery,
        IGetOrganizationReportDataQuery getOrganizationReportDataQuery,
        IUpdateOrganizationReportDataCommand updateOrganizationReportDataCommand,
        IGetOrganizationReportApplicationDataQuery getOrganizationReportApplicationDataQuery,
        IUpdateOrganizationReportApplicationDataCommand updateOrganizationReportApplicationDataCommand,
        IFeatureService featureService,
        IApplicationCacheService applicationCacheService,
        IOrganizationReportStorageService storageService,
        ICreateOrganizationReportCommand createReportCommand,
        IOrganizationReportRepository organizationReportRepo,
        IUpdateOrganizationReportV2Command updateReportV2Command,
        IValidateOrganizationReportFileCommand validateCommand,
        ILogger<OrganizationReportsController> logger)
    {
        _currentContext = currentContext;
        _getOrganizationReportQuery = getOrganizationReportQuery;
        _addOrganizationReportCommand = addOrganizationReportCommand;
        _updateOrganizationReportCommand = updateOrganizationReportCommand;
        _updateOrganizationReportSummaryCommand = updateOrganizationReportSummaryCommand;
        _getOrganizationReportSummaryDataQuery = getOrganizationReportSummaryDataQuery;
        _getOrganizationReportSummaryDataByDateRangeQuery = getOrganizationReportSummaryDataByDateRangeQuery;
        _getOrganizationReportDataQuery = getOrganizationReportDataQuery;
        _updateOrganizationReportDataCommand = updateOrganizationReportDataCommand;
        _getOrganizationReportApplicationDataQuery = getOrganizationReportApplicationDataQuery;
        _updateOrganizationReportApplicationDataCommand = updateOrganizationReportApplicationDataCommand;
        _featureService = featureService;
        _applicationCacheService = applicationCacheService;
        _storageService = storageService;
        _createReportCommand = createReportCommand;
        _organizationReportRepo = organizationReportRepo;
        _updateReportV2Command = updateReportV2Command;
        _validateCommand = validateCommand;
        _logger = logger;
    }


    [HttpGet("{organizationId}/latest")]
    public async Task<IActionResult> GetLatestOrganizationReportAsync(Guid organizationId)
    {
        if (_featureService.IsEnabled(FeatureFlagKeys.AccessIntelligenceVersion2))
        {
            await AuthorizeAsync(organizationId);

            var latestReport = await _getOrganizationReportQuery.GetLatestOrganizationReportAsync(organizationId);
            if (latestReport == null)
            {
                return Ok(null);
            }

            var response = new OrganizationReportResponseModel(latestReport);

            var fileData = latestReport.GetReportFile();
            if (fileData is { Validated: true })
            {
                response.ReportFileDownloadUrl = await _storageService.GetReportDataDownloadUrlAsync(latestReport, fileData);
            }

            return Ok(response);
        }

        if (!await _currentContext.AccessReports(organizationId))
        {
            throw new NotFoundException();
        }

        var v1LatestReport = await _getOrganizationReportQuery.GetLatestOrganizationReportAsync(organizationId);
        var v1Response = v1LatestReport == null ? null : new OrganizationReportResponseModel(v1LatestReport);

        return Ok(v1Response);
    }

    /**
    * Keeping post v2 launch of Access Intelligence
    **/

    // CREATE Whole Report
    [HttpPost("{organizationId}")]
    [RequestSizeLimit(Constants.FileSize501mb)]
    public async Task<IActionResult> CreateOrganizationReportAsync(
        Guid organizationId,
        [FromBody] AddOrganizationReportRequest request)
    {
        if (_featureService.IsEnabled(FeatureFlagKeys.AccessIntelligenceVersion2))
        {
            if (organizationId == Guid.Empty)
            {
                throw new BadRequestException("Organization ID is required.");
            }

            if (request.OrganizationId != organizationId)
            {
                throw new BadRequestException("Organization ID in the request body must match the route parameter");
            }

            if (!request.FileSize.HasValue)
            {
                throw new BadRequestException("File size is required.");
            }

            if (request.FileSize.Value > Constants.FileSize501mb)
            {
                throw new BadRequestException("Max file size is 500 MB.");
            }

            await AuthorizeAsync(organizationId);

            var report = await _createReportCommand.CreateAsync(request);
            var fileData = report.GetReportFile()!;

            return Ok(new OrganizationReportFileResponseModel
            {
                ReportFileUploadUrl = await _storageService.GetReportFileUploadUrlAsync(report, fileData),
                ReportResponse = new OrganizationReportResponseModel(report),
                FileUploadType = _storageService.FileUploadType
            });
        }

        if (!await _currentContext.AccessReports(organizationId))
        {
            throw new NotFoundException();
        }

        if (request.OrganizationId != organizationId)
        {
            throw new BadRequestException("Organization ID in the request body must match the route parameter");
        }

        var v1Report = await _addOrganizationReportCommand.AddOrganizationReportAsync(request);
        var response = v1Report == null ? null : new OrganizationReportResponseModel(v1Report);
        return Ok(response);
    }

    // READ Whole Report BY IDs
    [HttpGet("{organizationId}/{reportId}")]
    public async Task<IActionResult> GetOrganizationReportAsync(Guid organizationId, Guid reportId)
    {
        if (_featureService.IsEnabled(FeatureFlagKeys.AccessIntelligenceVersion2))
        {
            await AuthorizeAsync(organizationId);

            var report = await _getOrganizationReportQuery.GetOrganizationReportAsync(reportId);

            if (report == null)
            {
                throw new NotFoundException("Report not found for the specified organization.");
            }

            if (report.OrganizationId != organizationId)
            {
                throw new BadRequestException("Invalid report ID");
            }

            var response = new OrganizationReportResponseModel(report);

            var fileData = report.GetReportFile();
            if (fileData is { Validated: true })
            {
                response.ReportFileDownloadUrl = await _storageService.GetReportDataDownloadUrlAsync(report, fileData);
            }

            return Ok(response);
        }

        if (!await _currentContext.AccessReports(organizationId))
        {
            throw new NotFoundException();
        }

        var v1Report = await _getOrganizationReportQuery.GetOrganizationReportAsync(reportId);

        if (v1Report == null)
        {
            throw new NotFoundException("Report not found for the specified organization.");
        }

        if (v1Report.OrganizationId != organizationId)
        {
            throw new BadRequestException("Invalid report ID");
        }

        return Ok(v1Report);
    }

    // UPDATE Whole Report
    [HttpPatch("{organizationId}/{reportId}")]
    [RequestSizeLimit(Constants.FileSize501mb)]
    public async Task<IActionResult> UpdateOrganizationReportAsync(
        Guid organizationId,
        Guid reportId,
        [FromBody] UpdateOrganizationReportV2Request request)
    {
        if (_featureService.IsEnabled(FeatureFlagKeys.AccessIntelligenceVersion2))
        {
            await AuthorizeAsync(organizationId);

            request.OrganizationId = organizationId;
            request.ReportId = reportId;

            if (request.RequiresNewFileUpload)
            {
                if (!request.FileSize.HasValue)
                {
                    throw new BadRequestException("File size is required.");
                }

                if (request.FileSize.Value > Constants.FileSize501mb)
                {
                    throw new BadRequestException("Max file size is 500 MB.");
                }
            }

            var report = await _updateReportV2Command.UpdateAsync(request);

            if (request.RequiresNewFileUpload)
            {
                var fileData = report.GetReportFile()!;
                return Ok(new OrganizationReportFileResponseModel
                {
                    ReportFileUploadUrl = await _storageService.GetReportFileUploadUrlAsync(report, fileData),
                    ReportResponse = new OrganizationReportResponseModel(report),
                    FileUploadType = _storageService.FileUploadType
                });
            }

            return Ok(new OrganizationReportResponseModel(report));
        }

        if (!await _currentContext.AccessReports(organizationId))
        {
            throw new NotFoundException();
        }

        if (request.OrganizationId != organizationId)
        {
            throw new BadRequestException("Organization ID in the request body must match the route parameter");
        }

        var v1Request = new UpdateOrganizationReportRequest
        {
            ReportId = reportId,
            OrganizationId = organizationId,
            ReportData = request.ReportData,
            ContentEncryptionKey = request.ContentEncryptionKey,
            SummaryData = request.SummaryData,
            ApplicationData = request.ApplicationData
        };

        var updatedReport = await _updateOrganizationReportCommand.UpdateOrganizationReportAsync(v1Request);
        var response = new OrganizationReportResponseModel(updatedReport);
        return Ok(response);
    }

    /// <summary>
    /// Gets summary data for organization reports within a specified date range.
    /// The response is optimized for widget display by returning up to 6 entries that are
    /// evenly spaced across the date range, including the most recent entry.
    /// This allows the widget to show trends over time while ensuring the latest data point is always included.
    /// </summary>
    /// <param name="organizationId"></param>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <returns></returns>
    [HttpGet("{organizationId}/data/summary")]
    public async Task<IActionResult> GetOrganizationReportSummaryDataByDateRangeAsync(
        Guid organizationId, [FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
    {
        if (!await _currentContext.AccessReports(organizationId))
        {
            throw new NotFoundException();
        }

        if (organizationId == Guid.Empty)
        {
            throw new BadRequestException("Organization ID is required.");
        }

        var summaryDataList = await _getOrganizationReportSummaryDataByDateRangeQuery
            .GetOrganizationReportSummaryDataByDateRangeAsync(organizationId, startDate, endDate);

        return Ok(summaryDataList);
    }

    [RequireFeature(FeatureFlagKeys.AccessIntelligenceVersion2)]
    [HttpPost("{organizationId}/{reportId}/file/report-data")]
    [SelfHosted(SelfHostedOnly = true)]
    [RequestSizeLimit(Constants.FileSize501mb)]
    [DisableFormValueModelBinding]
    public async Task UploadReportFileAsync(Guid organizationId, Guid reportId, [FromQuery] string reportFileId)
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
        if (report == null)
        {
            throw new NotFoundException();
        }

        if (report.OrganizationId != organizationId)
        {
            throw new BadRequestException("Invalid report ID");
        }

        var fileData = report.GetReportFile();
        if (fileData == null || fileData.Id != reportFileId)
        {
            throw new NotFoundException();
        }

        await Request.GetFileAsync(async (stream) =>
        {
            await _storageService.UploadReportDataAsync(report, fileData, stream);
        });

        var leeway = 1024L * 1024L; // 1 MB
        var minimum = Math.Max(0, fileData.Size - leeway);
        var maximum = Math.Min(fileData.Size + leeway, Constants.FileSize501mb);
        var (valid, length) = await _storageService.ValidateFileAsync(report, fileData, minimum, maximum);
        if (!valid)
        {
            throw new BadRequestException("File received does not match expected constraints.");
        }

        fileData.Validated = true;
        fileData.Size = length;
        report.SetReportFile(fileData);
        report.RevisionDate = DateTime.UtcNow;
        await _organizationReportRepo.ReplaceAsync(report);
    }

    [AllowAnonymous]
    [RequireFeature(FeatureFlagKeys.AccessIntelligenceVersion2)]
    [HttpPost("file/validate/azure")]
    public async Task<ObjectResult> AzureValidateFile()
    {
        return await ApiHelpers.HandleAzureEvents(Request, new Dictionary<string, Func<Azure.Messaging.EventGrid.EventGridEvent, Task>>
        {
            {
                "Microsoft.Storage.BlobCreated", async (eventGridEvent) =>
                {
                    try
                    {
                        var blobName =
                            eventGridEvent.Subject.Split($"{AzureOrganizationReportStorageService.ContainerName}/blobs/")[1];
                        var reportId = AzureOrganizationReportStorageService.ReportIdFromBlobName(blobName);
                        var report = await _organizationReportRepo.GetByIdAsync(new Guid(reportId));
                        if (report == null)
                        {
                            if (_storageService is AzureOrganizationReportStorageService azureStorageService)
                            {
                                await azureStorageService.DeleteBlobAsync(blobName);
                            }

                            return;
                        }

                        var fileData = report.GetReportFile();
                        if (fileData == null)
                        {
                            return;
                        }

                        await _validateCommand.ValidateAsync(report, fileData.Id!);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Uncaught exception occurred while handling event grid event: {Event}",
                            JsonSerializer.Serialize(eventGridEvent));
                    }
                }
            }
        });
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

    // Removing post v2 launch
    [HttpPatch("{organizationId}/data/application/{reportId}")]
    public async Task<IActionResult> UpdateOrganizationReportApplicationDataAsync(Guid organizationId, Guid reportId, [FromBody] UpdateOrganizationReportApplicationDataRequest request)
    {
        try
        {
            if (!await _currentContext.AccessReports(organizationId))
            {
                throw new NotFoundException();
            }

            if (request.OrganizationId != organizationId)
            {
                throw new BadRequestException("Organization ID in the request body must match the route parameter");
            }

            if (request.Id != reportId)
            {
                throw new BadRequestException("Report ID in the request body must match the route parameter");
            }

            var updatedReport = await _updateOrganizationReportApplicationDataCommand.UpdateOrganizationReportApplicationDataAsync(request);
            var response = new OrganizationReportResponseModel(updatedReport);

            return Ok(response);
        }
        catch (Exception ex) when (!(ex is BadRequestException || ex is NotFoundException))
        {
            throw;
        }
    }

    [HttpGet("{organizationId}/data/application/{reportId}")]
    public async Task<IActionResult> GetOrganizationReportApplicationDataAsync(Guid organizationId, Guid reportId)
    {
        try
        {
            if (!await _currentContext.AccessReports(organizationId))
            {
                throw new NotFoundException();
            }

            var applicationData = await _getOrganizationReportApplicationDataQuery.GetOrganizationReportApplicationDataAsync(organizationId, reportId);

            if (applicationData == null)
            {
                throw new NotFoundException("Organization report application data not found.");
            }

            return Ok(applicationData);
        }
        catch (Exception ex) when (!(ex is BadRequestException || ex is NotFoundException))
        {
            throw;
        }
    }

    [HttpPatch("{organizationId}/data/report/{reportId}")]
    public async Task<IActionResult> UpdateOrganizationReportDataAsync(
        Guid organizationId,
        Guid reportId,
        [FromBody] UpdateOrganizationReportDataRequest request)
    {
        if (!await _currentContext.AccessReports(organizationId))
        {
            throw new NotFoundException();
        }

        if (request.OrganizationId != organizationId)
        {
            throw new BadRequestException("Organization ID in the request body must match the route parameter");
        }

        if (request.ReportId != reportId)
        {
            throw new BadRequestException("Report ID in the request body must match the route parameter");
        }

        var updatedReport = await _updateOrganizationReportDataCommand.UpdateOrganizationReportDataAsync(request);
        var response = new OrganizationReportResponseModel(updatedReport);

        return Ok(response);
    }

    [HttpGet("{organizationId}/data/report/{reportId}")]
    public async Task<IActionResult> GetOrganizationReportDataAsync(Guid organizationId, Guid reportId)
    {
        if (!await _currentContext.AccessReports(organizationId))
        {
            throw new NotFoundException();
        }

        var reportData = await _getOrganizationReportDataQuery.GetOrganizationReportDataAsync(organizationId, reportId);

        if (reportData == null)
        {
            throw new NotFoundException("Organization report data not found.");
        }

        return Ok(reportData);
    }

    [HttpPatch("{organizationId}/data/summary/{reportId}")]
    public async Task<IActionResult> UpdateOrganizationReportSummaryAsync(Guid organizationId, Guid reportId, [FromBody] UpdateOrganizationReportSummaryRequest request)
    {
        if (!await _currentContext.AccessReports(organizationId))
        {
            throw new NotFoundException();
        }

        if (request.OrganizationId != organizationId)
        {
            throw new BadRequestException("Organization ID in the request body must match the route parameter");
        }

        if (request.ReportId != reportId)
        {
            throw new BadRequestException("Report ID in the request body must match the route parameter");
        }
        var updatedReport = await _updateOrganizationReportSummaryCommand.UpdateOrganizationReportSummaryAsync(request);
        var response = new OrganizationReportResponseModel(updatedReport);

        return Ok(response);
    }

    [HttpGet("{organizationId}/data/summary/{reportId}")]
    public async Task<IActionResult> GetOrganizationReportSummaryAsync(Guid organizationId, Guid reportId)
    {
        if (!await _currentContext.AccessReports(organizationId))
        {
            throw new NotFoundException();
        }

        var summaryData =
            await _getOrganizationReportSummaryDataQuery.GetOrganizationReportSummaryDataAsync(organizationId, reportId);

        if (summaryData == null)
        {
            throw new NotFoundException("Report not found for the specified organization.");
        }

        return Ok(summaryData);
    }
}
