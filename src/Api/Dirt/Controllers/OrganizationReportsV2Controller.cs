using Bit.Api.Dirt.Models.Response;
using Bit.Core.Context;
using Bit.Core.Dirt.Models.Data;
using Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;
using Bit.Core.Dirt.Reports.ReportFeatures.Requests;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Dirt.Controllers;

[Route("reports/v2/organizations")]
[Authorize("Application")]
public class OrganizationReportsV2Controller : Controller
{
    private readonly ICurrentContext _currentContext;
    private readonly IApplicationCacheService _applicationCacheService;
    private readonly IGetOrganizationReportSummaryDataByDateRangeV2Query _getSummaryByDateRangeQuery;
    private readonly IGetOrganizationReportSummaryDataV2Query _getSummaryDataQuery;
    private readonly IUpdateOrganizationReportSummaryV2Command _updateSummaryCommand;

    public OrganizationReportsV2Controller(
        ICurrentContext currentContext,
        IApplicationCacheService applicationCacheService,
        IGetOrganizationReportSummaryDataByDateRangeV2Query getSummaryByDateRangeQuery,
        IGetOrganizationReportSummaryDataV2Query getSummaryDataQuery,
        IUpdateOrganizationReportSummaryV2Command updateSummaryCommand)
    {
        _currentContext = currentContext;
        _applicationCacheService = applicationCacheService;
        _getSummaryByDateRangeQuery = getSummaryByDateRangeQuery;
        _getSummaryDataQuery = getSummaryDataQuery;
        _updateSummaryCommand = updateSummaryCommand;
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

    #region SummaryData Field Endpoints

    [HttpGet("{organizationId}/data/summary")]
    public async Task<IEnumerable<OrganizationReportSummaryDataResponse>> GetOrganizationReportSummaryDataByDateRangeV2Async(
        Guid organizationId, [FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
    {
        if (organizationId == Guid.Empty) throw new BadRequestException("OrganizationId is required.");

        if (startDate == default) throw new BadRequestException("Start date is required.");

        if (endDate == default) throw new BadRequestException("End date is required.");

        if (startDate > endDate) throw new BadRequestException("Start date must be before or equal to end date.");

        await AuthorizeAsync(organizationId);

        return await _getSummaryByDateRangeQuery
            .GetSummaryDataByDateRangeAsync(organizationId, startDate, endDate);
    }

    [HttpGet("{organizationId}/data/summary/{reportId}")]
    public async Task<OrganizationReportSummaryDataResponse> GetOrganizationReportSummaryV2Async(
        Guid organizationId, Guid reportId)
    {
        if (organizationId == Guid.Empty) throw new BadRequestException("OrganizationId is required.");

        if (reportId == Guid.Empty) throw new BadRequestException("ReportId is required.");

        await AuthorizeAsync(organizationId);

        var summaryData = await _getSummaryDataQuery
            .GetSummaryDataAsync(organizationId, reportId);

        if (summaryData == null) throw new NotFoundException("Organization report summary data not found.");

        return summaryData;
    }

    [HttpPatch("{organizationId}/data/summary/{reportId}")]
    public async Task<OrganizationReportResponseModel> UpdateOrganizationReportSummaryV2Async(
        Guid organizationId, Guid reportId,
        [FromBody] UpdateOrganizationReportSummaryRequest request)
    {
        if (request.OrganizationId != organizationId) throw new BadRequestException("Organization ID in the request body must match the route parameter");

        if (request.ReportId != reportId) throw new BadRequestException("Report ID in the request body must match the route parameter");

        if (string.IsNullOrWhiteSpace(request.SummaryData)) throw new BadRequestException("Summary Data is required");

        await AuthorizeAsync(organizationId);

        var updatedReport = await _updateSummaryCommand
            .UpdateSummaryAsync(request);

        return new OrganizationReportResponseModel(updatedReport);
    }

    #endregion
}
