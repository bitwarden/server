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
    private readonly IGetOrganizationReportApplicationDataV2Query _getApplicationDataQuery;
    private readonly IUpdateOrganizationReportApplicationDataV2Command _updateApplicationDataCommand;

    public OrganizationReportsV2Controller(
        ICurrentContext currentContext,
        IApplicationCacheService applicationCacheService,
        IGetOrganizationReportApplicationDataV2Query getApplicationDataQuery,
        IUpdateOrganizationReportApplicationDataV2Command updateApplicationDataCommand)
    {
        _currentContext = currentContext;
        _applicationCacheService = applicationCacheService;
        _getApplicationDataQuery = getApplicationDataQuery;
        _updateApplicationDataCommand = updateApplicationDataCommand;
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
