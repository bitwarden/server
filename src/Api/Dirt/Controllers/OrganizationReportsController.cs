using System.Text.Json;
using Bit.Api.Dirt.Models.Request;
using Bit.Api.Dirt.Models.Response;
using Bit.Api.Utilities;
using Bit.Core;
using Bit.Core.Context;
using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Models.Data;
using Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;
using Bit.Core.Dirt.Reports.ReportFeatures.Requests;
using Bit.Core.Dirt.Reports.Services;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZiggyCreatures.Caching.Fusion;

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
    private readonly IFusionCache _cache;

    public OrganizationReportsController(
        ICurrentContext currentContext,
        IGetOrganizationReportQuery getOrganizationReportQuery,
        IAddOrganizationReportCommand addOrganizationReportCommand,
        IUpdateOrganizationReportCommand updateOrganizationReportCommand,
        IUpdateOrganizationReportSummaryCommand updateOrganizationReportSummaryCommand,
        IGetOrganizationReportSummaryDataQuery getOrganizationReportSummaryDataQuery,
        IGetOrganizationReportSummaryDataByDateRangeQuery getOrganizationReportSummaryDataByDateRangeQuery,
        IGetOrganizationReportApplicationDataQuery getOrganizationReportApplicationDataQuery,
        IUpdateOrganizationReportApplicationDataCommand updateOrganizationReportApplicationDataCommand,
        IFeatureService featureService,
        IApplicationCacheService applicationCacheService,
        IOrganizationReportStorageService storageService,
        ICreateOrganizationReportCommand createReportCommand,
        IOrganizationReportRepository organizationReportRepo,
        IUpdateOrganizationReportV2Command updateReportV2Command,
        IValidateOrganizationReportFileCommand validateCommand,
        ILogger<OrganizationReportsController> logger,
        [FromKeyedServices(OrganizationReportCacheConstants.CacheName)] IFusionCache cache)
    {
        _currentContext = currentContext;
        _getOrganizationReportQuery = getOrganizationReportQuery;
        _addOrganizationReportCommand = addOrganizationReportCommand;
        _updateOrganizationReportCommand = updateOrganizationReportCommand;
        _updateOrganizationReportSummaryCommand = updateOrganizationReportSummaryCommand;
        _getOrganizationReportSummaryDataQuery = getOrganizationReportSummaryDataQuery;
        _getOrganizationReportSummaryDataByDateRangeQuery = getOrganizationReportSummaryDataByDateRangeQuery;
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
        _cache = cache;
    }

    /// <summary>
    /// Creates a new organization report for the specified organization.
    /// When the Access Intelligence V2 feature flag is enabled, validates the file size and returns
    /// a presigned upload URL for the report file along with the created report metadata.
    /// Otherwise, creates the report with inline data.
    /// </summary>
    /// <param name="organizationId">The unique identifier of the organization.</param>
    /// <param name="request">The request model containing report data and optional file metadata.</param>
    /// <returns>An <see cref="OrganizationReportFileResponseModel"/> with upload URL when V2 is enabled,
    /// or an <see cref="OrganizationReportResponseModel"/> otherwise.</returns>
    [HttpPost("{organizationId}")]
    [RequestSizeLimit(Constants.FileSize501mb)]
    public async Task<IActionResult> CreateOrganizationReportAsync(
        Guid organizationId,
        [FromBody] AddOrganizationReportRequestModel request)
    {
        EnsureValidIds(organizationId);

        await AuthorizeAsync(organizationId);

        if (_featureService.IsEnabled(FeatureFlagKeys.AccessIntelligenceVersion2))
        {
            if (!request.FileSize.HasValue)
            {
                throw new BadRequestException("File size is required.");
            }

            if (request.FileSize.Value > Constants.FileSize501mb)
            {
                throw new BadRequestException("Max file size is 500 MB.");
            }

            var report = await _createReportCommand.CreateAsync(request.ToData(organizationId));
            var fileData = report.GetReportFile()!;
            var reportFileUploadUrl = await _storageService.GetReportFileUploadUrlAsync(report, fileData);

            return Ok(new OrganizationReportFileResponseModel
            {
                ReportFileUploadUrl = reportFileUploadUrl,
                ReportResponse = new OrganizationReportResponseModel(report),
                FileUploadType = _storageService.FileUploadType
            });
        }

        var v1Report = await _addOrganizationReportCommand.AddOrganizationReportAsync(request.ToData(organizationId));
        var response = v1Report == null ? null : new OrganizationReportResponseModel(v1Report);
        return Ok(response);
    }


    /// <summary>
    /// Gets the most recent organization report for the specified organization.
    /// Includes a presigned download URL for the report file if one has been validated.
    /// </summary>
    /// <param name="organizationId">The unique identifier of the organization.</param>
    /// <returns>An <see cref="OrganizationReportResponseModel"/> for the most recent report.</returns>
    [HttpGet("{organizationId}/latest")]
    public async Task<IActionResult> GetLatestOrganizationReportAsync(Guid organizationId)
    {
        EnsureValidIds(organizationId);

        await AuthorizeAsync(organizationId);

        var latestReport = await _getOrganizationReportQuery.GetLatestOrganizationReportAsync(organizationId);

        if (latestReport == null)
        {
            throw new NotFoundException();
        }

        var response = new OrganizationReportResponseModel(latestReport);

        if (_featureService.IsEnabled(FeatureFlagKeys.AccessIntelligenceVersion2))
        {
            var fileData = latestReport.GetReportFile();
            if (fileData is { Validated: true })
            {
                response.ReportFileDownloadUrl = await _storageService.GetReportDataDownloadUrlAsync(latestReport, fileData);
            }
        }

        return Ok(response);
    }

    /// <summary>
    /// Gets a specific organization report by its report ID.
    /// Validates that the report belongs to the specified organization.
    /// Includes a presigned download URL for the report file if one has been validated.
    /// </summary>
    /// <param name="organizationId">The unique identifier of the organization.</param>
    /// <param name="reportId">The unique identifier of the report to retrieve.</param>
    /// <returns>An <see cref="OrganizationReportResponseModel"/> matching the specified IDs.</returns>
    [HttpGet("{organizationId}/{reportId}")]
    public async Task<IActionResult> GetOrganizationReportAsync(Guid organizationId, Guid reportId)
    {
        var report = await GetAuthorizedReportAsync(organizationId, reportId);

        var response = new OrganizationReportResponseModel(report);

        var fileData = report.GetReportFile();
        if (fileData == null)
        {
            return Ok(response);
        }

        if (fileData.Validated)
        {
            response.ReportFileDownloadUrl = await _storageService.GetReportDataDownloadUrlAsync(report, fileData);
        }

        return Ok(response);
    }

    /// <summary>
    /// Updates an existing organization report's metadata for the specified organization.
    /// Updates fields such as summary data, application data, metrics, and content encryption key.
    /// To create a new report with a file upload, use the POST endpoint instead.
    /// </summary>
    /// <param name="organizationId">The unique identifier of the organization.</param>
    /// <param name="reportId">The unique identifier of the report to update.</param>
    /// <param name="request">The request model containing updated report data.</param>
    /// <returns>An <see cref="OrganizationReportResponseModel"/> with the updated report.</returns>
    [HttpPatch("{organizationId}/{reportId}")]
    public async Task<IActionResult> UpdateOrganizationReportAsync(
        Guid organizationId,
        Guid reportId,
        [FromBody] UpdateOrganizationReportV2RequestModel request)
    {
        EnsureValidIds(organizationId, reportId);

        await AuthorizeAsync(organizationId);

        if (_featureService.IsEnabled(FeatureFlagKeys.AccessIntelligenceVersion2))
        {
            var coreRequest = request.ToData(organizationId, reportId);
            var report = await _updateReportV2Command.UpdateAsync(coreRequest);
            return Ok(new OrganizationReportResponseModel(report));
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
    /// Returns all report summary entries within the range.
    /// </summary>
    /// <param name="organizationId">The unique identifier of the organization.</param>
    /// <param name="startDate">The start of the date range to query.</param>
    /// <param name="endDate">The end of the date range to query.</param>
    /// <returns>A collection of summary data entries within the date range.</returns>
    /// <exception cref="NotFoundException"></exception>
    /// <exception cref="BadRequestException"></exception>
    [HttpGet("{organizationId}/data/summary")]
    [ProducesResponseType<IEnumerable<OrganizationReportSummaryDataResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrganizationReportSummaryDataByDateRangeAsync(
        Guid organizationId, [FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
    {
        EnsureValidIds(organizationId);

        await AuthorizeAsync(organizationId);

        var summaryDataList = await _getOrganizationReportSummaryDataByDateRangeQuery
            .GetOrganizationReportSummaryDataByDateRangeAsync(organizationId, startDate, endDate);

        return Ok(summaryDataList.Select(s => new OrganizationReportSummaryDataResponseModel(s)));
    }

    /// <summary>
    /// Deletes an organization report and its associated file from storage.
    /// Removes the database record first, then cleans up any stored files.
    /// </summary>
    /// <param name="organizationId">The unique identifier of the organization.</param>
    /// <param name="reportId">The unique identifier of the report to delete.</param>
    [HttpDelete("{organizationId}/{reportId}")]
    public async Task DeleteOrganizationReportAsync(Guid organizationId, Guid reportId)
    {
        var report = await GetAuthorizedReportAsync(organizationId, reportId);

        var fileData = report.GetReportFile();

        await _organizationReportRepo.DeleteAsync(report);

        await _cache.RemoveByTagAsync(
            OrganizationReportCacheConstants.BuildCacheTagForOrganizationReports(organizationId));

        if (fileData != null && !string.IsNullOrEmpty(fileData.Id))
        {
            try
            {
                await _storageService.DeleteReportFilesAsync(report, fileData.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to delete storage files for report {ReportId}, file {FileId}. Manual cleanup may be required.",
                    reportId, fileData.Id);
            }
        }
    }

    /// <summary>
    /// Renews the file upload URL for an organization report that has not yet been validated.
    /// Returns a fresh presigned upload URL for the report file, allowing the client to retry
    /// an upload after the original URL has expired. Requires the Access Intelligence V2 feature flag.
    /// </summary>
    /// <param name="organizationId">The unique identifier of the organization.</param>
    /// <param name="reportId">The unique identifier of the report with the pending file upload.</param>
    /// <param name="reportFileId">The identifier of the report file entry to renew the upload URL for.</param>
    /// <returns>An <see cref="OrganizationReportFileResponseModel"/> with the renewed upload URL.</returns>
    [RequireFeature(FeatureFlagKeys.AccessIntelligenceVersion2)]
    [HttpGet("{organizationId}/{reportId}/file/renew")]
    public async Task<OrganizationReportFileResponseModel> RenewFileUploadUrlAsync(
        Guid organizationId, Guid reportId, [FromQuery] string reportFileId)
    {
        var report = await GetAuthorizedReportAsync(organizationId, reportId);

        if (string.IsNullOrEmpty(reportFileId))
        {
            throw new BadRequestException("ReportFileId is required.");
        }

        var fileData = report.GetReportFile();
        if (fileData == null || fileData.Id != reportFileId || fileData.Validated)
        {
            throw new NotFoundException();
        }

        return new OrganizationReportFileResponseModel
        {
            ReportFileUploadUrl = await _storageService.GetReportFileUploadUrlAsync(report, fileData),
            ReportResponse = new OrganizationReportResponseModel(report),
            FileUploadType = _storageService.FileUploadType
        };
    }

    /// <summary>
    /// Handles Azure Event Grid webhook notifications for blob storage events.
    /// When a <c>Microsoft.Storage.BlobCreated</c> event is received, validates the uploaded
    /// report file against the corresponding organization report. Orphaned blobs (with no
    /// matching report) are deleted. Requires the Access Intelligence V2 feature flag.
    /// This endpoint is anonymous to allow Azure Event Grid to call it directly.
    /// </summary>
    /// <returns>An <see cref="ObjectResult"/> acknowledging the Event Grid event.</returns>
    [AllowAnonymous]
    [RequireFeature(FeatureFlagKeys.AccessIntelligenceVersion2)]
    [HttpPost("file/validate/azure")]
    public async Task<ObjectResult> AzureValidateFileAsync()
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

    /// <summary>
    /// Uploads a report data file for a self-hosted organization report via multipart form data.
    /// Validates the uploaded file size against the expected size (with a 1 MB leeway) and marks
    /// the report file as validated upon success. Requires the Access Intelligence V2 feature flag.
    /// </summary>
    /// <param name="organizationId">The unique identifier of the organization.</param>
    /// <param name="reportId">The unique identifier of the report to attach the file to.</param>
    /// <param name="reportFileId">The identifier of the report file entry to upload against.</param>
    [RequireFeature(FeatureFlagKeys.AccessIntelligenceVersion2)]
    [HttpPost("{organizationId}/{reportId}/file")]
    [SelfHosted(SelfHostedOnly = true)]
    [RequestSizeLimit(Constants.FileSize501mb)]
    [DisableFormValueModelBinding]
    public async Task UploadReportFileAsync(Guid organizationId, Guid reportId, [FromQuery] string reportFileId)
    {
        var report = await GetAuthorizedReportAsync(organizationId, reportId);

        if (!Request?.ContentType?.Contains("multipart/") ?? true)
        {
            throw new BadRequestException("Invalid content.");
        }

        if (string.IsNullOrEmpty(reportFileId))
        {
            throw new BadRequestException("ReportFileId query parameter is required");
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
            await _storageService.DeleteReportFilesAsync(report, fileData.Id!);
            await _organizationReportRepo.DeleteAsync(report);
            await _cache.RemoveByTagAsync(
                OrganizationReportCacheConstants.BuildCacheTagForOrganizationReports(organizationId));
            throw new BadRequestException("File received does not match expected constraints.");
        }

        fileData.Validated = true;
        fileData.Size = length;
        report.SetReportFile(fileData);
        report.RevisionDate = DateTime.UtcNow;
        await _organizationReportRepo.ReplaceAsync(report);
        await _cache.RemoveByTagAsync(
            OrganizationReportCacheConstants.BuildCacheTagForOrganizationReports(organizationId));
    }

    /// <summary>
    /// Downloads an organization report file for a self-hosted instance.
    /// Validates that the organization ID and report ID are non-empty,
    /// then authorizes the caller via <see cref="AuthorizeAsync"/>.
    /// Verifies the report exists and belongs to the specified organization.
    /// Retrieves the file metadata and streams the file from local storage.
    /// Cloud-hosted instances download files directly from Azure Blob Storage
    /// using presigned SAS URLs and never call this endpoint.
    /// </summary>
    /// <param name="organizationId">The unique identifier of the organization.</param>
    /// <param name="reportId">The unique identifier of the report whose file to download.</param>
    /// <returns>A <see cref="FileStreamResult"/> containing the report file with content type application/octet-stream.</returns>
    [SelfHosted(SelfHostedOnly = true)]
    [HttpGet("{organizationId}/{reportId}/file/download")]
    public async Task<IActionResult> DownloadReportFileAsync(Guid organizationId, Guid reportId)
    {
        var report = await GetAuthorizedReportAsync(organizationId, reportId);

        var fileData = report.GetReportFile();
        if (fileData == null)
        {
            throw new NotFoundException();
        }

        var stream = await _storageService.GetReportReadStreamAsync(report, fileData);
        if (stream == null)
        {
            throw new NotFoundException();
        }

        return File(stream, "application/octet-stream", fileData.FileName);
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

    private static void EnsureValidIds(Guid organizationId, Guid? reportId = null)
    {
        if (organizationId == Guid.Empty)
        {
            throw new BadRequestException("OrganizationId is required.");
        }

        if (reportId.HasValue && reportId.Value == Guid.Empty)
        {
            throw new BadRequestException("ReportId is required.");
        }
    }

    private async Task<OrganizationReport> GetAuthorizedReportAsync(Guid organizationId, Guid reportId)
    {
        EnsureValidIds(organizationId, reportId);
        await AuthorizeAsync(organizationId);
        var report = await _getOrganizationReportQuery.GetOrganizationReportAsync(reportId);
        if (report.OrganizationId != organizationId) throw new BadRequestException("Invalid report ID");
        return report;
    }


    // Is being used by client on V2

    [HttpGet("{organizationId}/data/summary/{reportId}")]
    public async Task<IActionResult> GetOrganizationReportSummaryAsync(Guid organizationId, Guid reportId)
    {
        EnsureValidIds(organizationId, reportId);

        await AuthorizeAsync(organizationId);

        var summaryData =
            await _getOrganizationReportSummaryDataQuery.GetOrganizationReportSummaryDataAsync(organizationId, reportId);

        if (summaryData == null)
        {
            throw new NotFoundException("Report not found for the specified organization.");
        }

        return Ok(new OrganizationReportSummaryDataResponseModel(summaryData));
    }

    [HttpPatch("{organizationId}/data/summary/{reportId}")]
    public async Task<IActionResult> UpdateOrganizationReportSummaryAsync(
        Guid organizationId,
        Guid reportId,
        [FromBody] UpdateOrganizationReportSummaryRequestModel request)
    {
        EnsureValidIds(organizationId, reportId);

        await AuthorizeAsync(organizationId);

        var updatedReport = await _updateOrganizationReportSummaryCommand
            .UpdateOrganizationReportSummaryAsync(request.ToData(organizationId, reportId));
        var response = new OrganizationReportResponseModel(updatedReport);

        return Ok(response);
    }

    [HttpGet("{organizationId}/data/application/{reportId}")]
    public async Task<IActionResult> GetOrganizationReportApplicationDataAsync(Guid organizationId, Guid reportId)
    {
        EnsureValidIds(organizationId, reportId);

        await AuthorizeAsync(organizationId);

        var applicationData = await _getOrganizationReportApplicationDataQuery
            .GetOrganizationReportApplicationDataAsync(organizationId, reportId);

        if (applicationData == null)
        {
            throw new NotFoundException("Organization report application data not found.");
        }

        return Ok(new OrganizationReportApplicationDataResponseModel(applicationData));
    }


    [HttpPatch("{organizationId}/data/application/{reportId}")]
    public async Task<IActionResult> UpdateOrganizationReportApplicationDataAsync(
        Guid organizationId,
        Guid reportId,
        [FromBody] UpdateOrganizationReportApplicationDataRequestModel request)
    {
        EnsureValidIds(organizationId, reportId);

        await AuthorizeAsync(organizationId);

        var updatedReport = await _updateOrganizationReportApplicationDataCommand
            .UpdateOrganizationReportApplicationDataAsync(request.ToData(organizationId, reportId));
        var response = new OrganizationReportResponseModel(updatedReport);

        return Ok(response);
    }
}
