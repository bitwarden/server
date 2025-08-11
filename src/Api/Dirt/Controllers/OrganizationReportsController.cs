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

    public OrganizationReportsController(
        ICurrentContext currentContext,
        IGetOrganizationReportQuery getOrganizationReportQuery,
        IAddOrganizationReportCommand addOrganizationReportCommand,
        IUpdateOrganizationReportCommand updateOrganizationReportCommand
    )
    {
        _currentContext = currentContext;
        _getOrganizationReportQuery = getOrganizationReportQuery;
        _addOrganizationReportCommand = addOrganizationReportCommand;
        _updateOrganizationReportCommand = updateOrganizationReportCommand;
    }

    [HttpGet("{orgId}")]
    public async Task<IActionResult> GetOrganizationReportAsync(Guid orgId)
    {
        GuardOrganizationAccess(orgId);

        var reports = await _getOrganizationReportQuery.GetOrganizationReportAsync(orgId);
        return Ok(reports);
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

    [HttpPatch("{orgId}")]
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

    private void GuardOrganizationAccess(Guid organizationId)
    {
        if (!_currentContext.AccessReports(organizationId).Result)
        {
            throw new NotFoundException();
        }
    }
}
