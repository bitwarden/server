using Bit.Api.Dirt.Models.Response;
using Bit.Core;
using Bit.Core.Context;
using Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;
using Bit.Core.Dirt.Reports.ReportFeatures.Requests;
using Bit.Core.Exceptions;
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
        IUpdateOrganizationReportApplicationDataCommand updateOrganizationReportApplicationDataCommand
    )
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
    }

    #region Whole OrganizationReport Endpoints

    [HttpGet("{organizationId}/latest")]
    public async Task<IActionResult> GetLatestOrganizationReportAsync(Guid organizationId)
    {
        if (!await _currentContext.AccessReports(organizationId))
        {
            throw new NotFoundException();
        }

        var latestReport = await _getOrganizationReportQuery.GetLatestOrganizationReportAsync(organizationId);
        var response = latestReport == null ? null : new OrganizationReportResponseModel(latestReport);

        return Ok(response);
    }

    [HttpGet("{organizationId}/latest/download")]
    public async Task<IActionResult> DownloadLatestOrganizationReportAsync(Guid organizationId)
    {
        if (!await _currentContext.AccessReports(organizationId))
        {
            throw new NotFoundException();
        }

        var latestReport = await _getOrganizationReportQuery.GetLatestOrganizationReportAsync(organizationId);

        if (latestReport == null)
        {
            throw new NotFoundException("No report found for the specified organization.");
        }

        var stream = new MemoryStream();
        using (var writer = new StreamWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write("{");
            writer.Write($"\"id\":\"{latestReport.Id}\",");
            writer.Write($"\"organizationId\":\"{latestReport.OrganizationId}\",");

            // ReportData is an encrypted string - needs to be JSON-encoded as a string value
            if (latestReport.ReportData != null)
            {
                var escapedReportData = EscapeJsonString(latestReport.ReportData);
                writer.Write($"\"reportData\":\"{escapedReportData}\",");
            }
            else
            {
                writer.Write("\"reportData\":null,");
            }

            // ContentEncryptionKey is a string value
            var escapedKey = EscapeJsonString(latestReport.ContentEncryptionKey);
            writer.Write($"\"contentEncryptionKey\":\"{escapedKey}\",");

            // SummaryData is an encrypted string - needs to be JSON-encoded as a string value
            if (latestReport.SummaryData != null)
            {
                var escapedSummaryData = EscapeJsonString(latestReport.SummaryData);
                writer.Write($"\"summaryData\":\"{escapedSummaryData}\",");
            }
            else
            {
                writer.Write("\"summaryData\":null,");
            }

            // ApplicationData is an encrypted string - needs to be JSON-encoded as a string value
            if (latestReport.ApplicationData != null)
            {
                var escapedApplicationData = EscapeJsonString(latestReport.ApplicationData);
                writer.Write($"\"applicationData\":\"{escapedApplicationData}\",");
            }
            else
            {
                writer.Write("\"applicationData\":null,");
            }

            writer.Write($"\"creationDate\":\"{latestReport.CreationDate:o}\",");
            writer.Write($"\"revisionDate\":\"{latestReport.RevisionDate:o}\"");
            writer.Write("}");
        }
        stream.Position = 0;

        return File(stream, "application/json", $"organization-report-{latestReport.Id}.json");
    }

    [HttpGet("{organizationId}/{reportId}")]
    public async Task<IActionResult> GetOrganizationReportAsync(Guid organizationId, Guid reportId)
    {
        if (!await _currentContext.AccessReports(organizationId))
        {
            throw new NotFoundException();
        }

        var report = await _getOrganizationReportQuery.GetOrganizationReportAsync(reportId);

        if (report == null)
        {
            throw new NotFoundException("Report not found for the specified organization.");
        }

        if (report.OrganizationId != organizationId)
        {
            throw new BadRequestException("Invalid report ID");
        }

        return Ok(report);
    }

    [HttpPost("{organizationId}")]
    [RequestSizeLimit(Constants.FileSize501mb)]
    public async Task<IActionResult> CreateOrganizationReportAsync(Guid organizationId, [FromBody] AddOrganizationReportRequest request)
    {
        if (!await _currentContext.AccessReports(organizationId))
        {
            throw new NotFoundException();
        }

        if (request.OrganizationId != organizationId)
        {
            throw new BadRequestException("Organization ID in the request body must match the route parameter");
        }

        var report = await _addOrganizationReportCommand.AddOrganizationReportAsync(request);
        var response = report == null ? null : new OrganizationReportIdResponseModel(report);
        return Ok(response);
    }

    [HttpPatch("{organizationId}/{reportId}")]
    public async Task<IActionResult> UpdateOrganizationReportAsync(Guid organizationId, [FromBody] UpdateOrganizationReportRequest request)
    {
        if (!await _currentContext.AccessReports(organizationId))
        {
            throw new NotFoundException();
        }

        if (request.OrganizationId != organizationId)
        {
            throw new BadRequestException("Organization ID in the request body must match the route parameter");
        }

        var updatedReport = await _updateOrganizationReportCommand.UpdateOrganizationReportAsync(request);
        var response = new OrganizationReportResponseModel(updatedReport);
        return Ok(response);
    }

    #endregion

    # region SummaryData Field Endpoints

    [HttpGet("{organizationId}/data/summary")]
    public async Task<IActionResult> GetOrganizationReportSummaryDataByDateRangeAsync(
        Guid organizationId, [FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
    {
        if (!await _currentContext.AccessReports(organizationId))
        {
            throw new NotFoundException();
        }

        if (organizationId.Equals(null))
        {
            throw new BadRequestException("Organization ID is required.");
        }

        var summaryDataList = await _getOrganizationReportSummaryDataByDateRangeQuery
            .GetOrganizationReportSummaryDataByDateRangeAsync(organizationId, startDate, endDate);

        return Ok(summaryDataList);
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
    #endregion

    #region ReportData Field Endpoints

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

    [HttpPatch("{organizationId}/data/report/{reportId}")]
    [RequestSizeLimit(Constants.FileSize501mb)]
    public async Task<IActionResult> UpdateOrganizationReportDataAsync(Guid organizationId, Guid reportId, [FromBody] UpdateOrganizationReportDataRequest request)
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
        var response = new OrganizationReportIdResponseModel(updatedReport);

        return Ok(response);
    }

    #endregion

    #region ApplicationData Field Endpoints

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

    #endregion

    private static string EscapeJsonString(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t")
            .Replace("\b", "\\b")
            .Replace("\f", "\\f");
    }
}
