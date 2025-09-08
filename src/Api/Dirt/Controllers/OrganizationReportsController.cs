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

    [HttpGet("{orgId}/latest")]
    public async Task<IActionResult> GetLatestOrganizationReportAsync(Guid orgId)
    {
        if (!await _currentContext.AccessReports(orgId))
        {
            throw new NotFoundException();
        }

        var latestReport = await _getOrganizationReportQuery.GetLatestOrganizationReportAsync(orgId);

        return Ok(latestReport);
    }

    [HttpGet("{orgId}/{reportId}")]
    public async Task<IActionResult> GetOrganizationReportAsync(Guid orgId, Guid reportId)
    {
        if (!await _currentContext.AccessReports(orgId))
        {
            throw new NotFoundException();
        }

        var report = await _getOrganizationReportQuery.GetOrganizationReportAsync(reportId);

        if (report == null)
        {
            throw new NotFoundException("Report not found for the specified organization.");
        }

        if (report.OrganizationId != orgId)
        {
            throw new BadRequestException("Invalid report ID");
        }

        return Ok(report);
    }

    [HttpPost("{orgId}")]
    public async Task<IActionResult> CreateOrganizationReportAsync(Guid orgId, [FromBody] AddOrganizationReportRequest request)
    {
        if (!await _currentContext.AccessReports(orgId))
        {
            throw new NotFoundException();
        }

        if (request.OrganizationId != orgId)
        {
            throw new BadRequestException("Organization ID in the request body must match the route parameter");
        }

        var report = await _addOrganizationReportCommand.AddOrganizationReportAsync(request);
        return Ok(report);
    }

    [HttpPatch("{orgId}/{reportId}")]
    public async Task<IActionResult> UpdateOrganizationReportAsync(Guid orgId, [FromBody] UpdateOrganizationReportRequest request)
    {
        if (!await _currentContext.AccessReports(orgId))
        {
            throw new NotFoundException();
        }

        if (request.OrganizationId != orgId)
        {
            throw new BadRequestException("Organization ID in the request body must match the route parameter");
        }

        var updatedReport = await _updateOrganizationReportCommand.UpdateOrganizationReportAsync(request);
        return Ok(updatedReport);
    }

    #endregion

    # region SummaryData Field Endpoints

    [HttpGet("{orgId}/data/summary")]
    public async Task<IActionResult> GetOrganizationReportSummaryDataByDateRangeAsync(
        Guid orgId, [FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
    {
        if (!await _currentContext.AccessReports(orgId))
        {
            throw new NotFoundException();
        }

        if (orgId.Equals(null))
        {
            throw new BadRequestException("Organization ID is required.");
        }

        var summaryDataList = await _getOrganizationReportSummaryDataByDateRangeQuery
            .GetOrganizationReportSummaryDataByDateRangeAsync(orgId, startDate, endDate);

        return Ok(summaryDataList);
    }

    [HttpGet("{orgId}/data/summary/{reportId}")]
    public async Task<IActionResult> GetOrganizationReportSummaryAsync(Guid orgId, Guid reportId)
    {
        if (!await _currentContext.AccessReports(orgId))
        {
            throw new NotFoundException();
        }

        var summaryData =
            await _getOrganizationReportSummaryDataQuery.GetOrganizationReportSummaryDataAsync(orgId, reportId);

        if (summaryData == null)
        {
            throw new NotFoundException("Report not found for the specified organization.");
        }

        return Ok(summaryData);
    }

    [HttpPatch("{orgId}/data/summary/{reportId}")]
    public async Task<IActionResult> UpdateOrganizationReportSummaryAsync(Guid orgId, Guid reportId, [FromBody] UpdateOrganizationReportSummaryRequest request)
    {
        if (!await _currentContext.AccessReports(orgId))
        {
            throw new NotFoundException();
        }

        if (request.OrganizationId != orgId)
        {
            throw new BadRequestException("Organization ID in the request body must match the route parameter");
        }

        if (request.ReportId != reportId)
        {
            throw new BadRequestException("Report ID in the request body must match the route parameter");
        }

        var updatedReport = await _updateOrganizationReportSummaryCommand.UpdateOrganizationReportSummaryAsync(request);

        return Ok(updatedReport);
    }
    #endregion

    #region ReportData Field Endpoints

    [HttpGet("{orgId}/data/report/{reportId}")]
    public async Task<IActionResult> GetOrganizationReportDataAsync(Guid orgId, Guid reportId)
    {
        if (!await _currentContext.AccessReports(orgId))
        {
            throw new NotFoundException();
        }

        var reportData = await _getOrganizationReportDataQuery.GetOrganizationReportDataAsync(orgId, reportId);

        if (reportData == null)
        {
            throw new NotFoundException("Organization report data not found.");
        }

        return Ok(reportData);
    }

    [HttpPatch("{orgId}/data/report/{reportId}")]
    public async Task<IActionResult> UpdateOrganizationReportDataAsync(Guid orgId, Guid reportId, [FromBody] UpdateOrganizationReportDataRequest request)
    {
        if (!await _currentContext.AccessReports(orgId))
        {
            throw new NotFoundException();
        }

        if (request.OrganizationId != orgId)
        {
            throw new BadRequestException("Organization ID in the request body must match the route parameter");
        }

        if (request.ReportId != reportId)
        {
            throw new BadRequestException("Report ID in the request body must match the route parameter");
        }

        var updatedReport = await _updateOrganizationReportDataCommand.UpdateOrganizationReportDataAsync(request);
        return Ok(updatedReport);
    }

    #endregion

    #region ApplicationData Field Endpoints

    [HttpGet("{orgId}/data/application/{reportId}")]
    public async Task<IActionResult> GetOrganizationReportApplicationDataAsync(Guid orgId, Guid reportId)
    {
        try
        {
            if (!await _currentContext.AccessReports(orgId))
            {
                throw new NotFoundException();
            }

            var applicationData = await _getOrganizationReportApplicationDataQuery.GetOrganizationReportApplicationDataAsync(orgId, reportId);

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

    [HttpPatch("{orgId}/data/application/{reportId}")]
    public async Task<IActionResult> UpdateOrganizationReportApplicationDataAsync(Guid orgId, Guid reportId, [FromBody] UpdateOrganizationReportApplicationDataRequest request)
    {
        try
        {

            if (!await _currentContext.AccessReports(orgId))
            {
                throw new NotFoundException();
            }

            if (request.OrganizationId != orgId)
            {
                throw new BadRequestException("Organization ID in the request body must match the route parameter");
            }

            if (request.Id != reportId)
            {
                throw new BadRequestException("Report ID in the request body must match the route parameter");
            }

            var updatedReport = await _updateOrganizationReportApplicationDataCommand.UpdateOrganizationReportApplicationDataAsync(request);



            return Ok(updatedReport);
        }
        catch (Exception ex) when (!(ex is BadRequestException || ex is NotFoundException))
        {
            throw;
        }
    }

    #endregion
}
