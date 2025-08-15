using Bit.Core.Context;
using Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;
using Bit.Core.Dirt.Reports.ReportFeatures.Requests;
using Bit.Core.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Dirt.Controllers;

[Route("reports/organization")]
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
        GuardOrganizationAccess(orgId);

        var latestReport = await _getOrganizationReportQuery.GetLatestOrganizationReportAsync(orgId);

        return Ok(latestReport);
    }

    [HttpGet("{orgId}/{reportId}")]
    public async Task<IActionResult> GetOrganizationReportAsync(Guid orgId, Guid reportId)
    {
        GuardOrganizationAccess(orgId);

        var report = await _getOrganizationReportQuery.GetOrganizationReportAsync(reportId);

        if (report == null)
        {
            throw new NotFoundException("Report not found for the specified organization.");
        }

        if (report.OrganizationId != orgId)
        {
            throw new NotFoundException("Report not found for the specified organization.");
        }

        return Ok(report);
    }

    [HttpPost("{orgId}")]
    public async Task<IActionResult> CreateOrganizationReportAsync(Guid orgId, [FromBody] AddOrganizationReportRequest request)
    {
        GuardOrganizationAccess(orgId);

        if (request.OrganizationId != orgId)
        {
            throw new BadRequestException("Organization ID in the request body must match the route parameter");
        }

        var report = await _addOrganizationReportCommand.AddOrganizationReportAsync(request);
        return Ok(report);
    }

    [HttpPatch("{orgId}/")]
    public async Task<IActionResult> UpdateOrganizationReportAsync(Guid orgId, [FromBody] UpdateOrganizationReportRequest request)
    {
        GuardOrganizationAccess(orgId);

        if (request.OrganizationId != orgId)
        {
            throw new BadRequestException("Organization ID in the request body must match the route parameter");
        }

        var updatedReport = await _updateOrganizationReportCommand.UpdateOrganizationReportAsync(request);
        return Ok(updatedReport);
    }

    #endregion

    # region SummaryData Field Endpoints

    [HttpGet("{orgId}/data/summary/{reportId}/date-range")]
    public async Task<IActionResult> GetOrganizationReportSummaryDataByDateRangeAsync(
        Guid orgId,
        Guid reportId,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate)
    {
        GuardOrganizationAccess(orgId);

        var summaryDataList = await _getOrganizationReportSummaryDataByDateRangeQuery
            .GetOrganizationReportSummaryDataByDateRangeAsync(orgId, reportId, startDate, endDate);

        return Ok(summaryDataList);
    }

    [HttpGet("{orgId}/data/summary/{reportId}")]
    public async Task<IActionResult> GetOrganizationReportSummaryAsync(Guid orgId, Guid reportId)
    {
        GuardOrganizationAccess(orgId);

        var summaryData =
            await _getOrganizationReportSummaryDataQuery.GetOrganizationReportSummaryDataAsync(orgId, reportId);


        if (summaryData == null)
        {
            throw new NotFoundException("Report not found for the specified organization.");
        }

        if (summaryData.OrganizationId != orgId)
        {
            throw new NotFoundException("Report not found for the specified organization.");
        }

        return Ok(summaryData);
    }

    [HttpPatch("{orgId}/data/summary")]
    public async Task<IActionResult> UpdateOrganizationReportSummaryAsync(Guid orgId, [FromBody] UpdateOrganizationReportSummaryRequest request)
    {
        GuardOrganizationAccess(request.OrganizationId);

        if (request.OrganizationId != orgId)
        {
            throw new BadRequestException("Organization ID in the request body must match the route parameter");
        }

        var updatedReport = await _updateOrganizationReportSummaryCommand.UpdateOrganizationReportSummaryAsync(request);

        return Ok(updatedReport);
    }
    #endregion

    #region ReportData Field Endpoints

    [HttpGet("{orgId}/data/report/{reportId}")]
    public async Task<IActionResult> GetOrganizationReportDataAsync(Guid orgId, Guid reportId)
    {
        GuardOrganizationAccess(orgId);

        var reportData = await _getOrganizationReportDataQuery.GetOrganizationReportDataAsync(orgId, reportId);
        return Ok(reportData);
    }

    [HttpPatch("{orgId}/data/report")]
    public async Task<IActionResult> UpdateOrganizationReportDataAsync(Guid orgId, [FromBody] UpdateOrganizationReportDataRequest request)
    {
        GuardOrganizationAccess(orgId);

        if (request.OrganizationId != orgId)
        {
            throw new BadRequestException("Organization ID in the request body must match the route parameter");
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
            GuardOrganizationAccess(orgId);

            var applicationData = await _getOrganizationReportApplicationDataQuery.GetOrganizationReportApplicationDataAsync(orgId, reportId);

            if (applicationData == null)
            {
                throw new NotFoundException("Organization report application data not found.");
            }

            if (applicationData.OrganizationId != orgId)
            {
                throw new NotFoundException("Report not found for the specified organization.");
            }

            return Ok(applicationData);
        }
        catch (Exception ex) when (!(ex is BadRequestException || ex is NotFoundException))
        {
            throw;
        }
    }

    [HttpPatch("{orgId}/data/application")]
    public async Task<IActionResult> UpdateOrganizationReportApplicationDataAsync(Guid orgId, [FromBody] UpdateOrganizationReportApplicationDataRequest request)
    {
        try
        {
            GuardOrganizationAccess(orgId);

            if (request.OrganizationId != orgId)
            {
                throw new BadRequestException("Organization ID in the request body must match the route parameter");
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


    private void GuardOrganizationAccess(Guid organizationId)
    {
        if (!_currentContext.AccessReports(organizationId).Result)
        {
            throw new NotFoundException();
        }
    }
}
