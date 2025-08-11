using Bit.Api.Dirt.Models;
using Bit.Api.Dirt.Models.Response;
using Bit.Api.Tools.Models.Response;
using Bit.Core.Context;
using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Reports.Models.Data;
using Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;
using Bit.Core.Dirt.Reports.ReportFeatures.OrganizationReportMembers.Interfaces;
using Bit.Core.Dirt.Reports.ReportFeatures.Requests;
using Bit.Core.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Dirt.Controllers;

[Route("reports")]
[Authorize("Application")]
public class ReportsController : Controller
{
    private readonly ICurrentContext _currentContext;
    private readonly IMemberAccessReportQuery _memberAccessReportQuery;
    private readonly IRiskInsightsReportQuery _riskInsightsReportQuery;
    private readonly IAddPasswordHealthReportApplicationCommand _addPwdHealthReportAppCommand;
    private readonly IGetPasswordHealthReportApplicationQuery _getPwdHealthReportAppQuery;
    private readonly IDropPasswordHealthReportApplicationCommand _dropPwdHealthReportAppCommand;
    private readonly IAddOrganizationReportCommand _addOrganizationReportCommand;
    private readonly IDropOrganizationReportCommand _dropOrganizationReportCommand;
    private readonly IGetOrganizationReportQuery _getOrganizationReportQuery;

    public ReportsController(
        ICurrentContext currentContext,
        IMemberAccessReportQuery memberAccessReportQuery,
        IRiskInsightsReportQuery riskInsightsReportQuery,
        IAddPasswordHealthReportApplicationCommand addPasswordHealthReportApplicationCommand,
        IGetPasswordHealthReportApplicationQuery getPasswordHealthReportApplicationQuery,
        IDropPasswordHealthReportApplicationCommand dropPwdHealthReportAppCommand,
        IGetOrganizationReportQuery getOrganizationReportQuery,
        IAddOrganizationReportCommand addOrganizationReportCommand,
        IDropOrganizationReportCommand dropOrganizationReportCommand
    )
    {
        _currentContext = currentContext;
        _memberAccessReportQuery = memberAccessReportQuery;
        _riskInsightsReportQuery = riskInsightsReportQuery;
        _addPwdHealthReportAppCommand = addPasswordHealthReportApplicationCommand;
        _getPwdHealthReportAppQuery = getPasswordHealthReportApplicationQuery;
        _dropPwdHealthReportAppCommand = dropPwdHealthReportAppCommand;
        _getOrganizationReportQuery = getOrganizationReportQuery;
        _addOrganizationReportCommand = addOrganizationReportCommand;
        _dropOrganizationReportCommand = dropOrganizationReportCommand;
    }

    /// <summary>
    /// Organization member information containing a list of cipher ids
    /// assigned
    /// </summary>
    /// <param name="orgId">Organzation Id</param>
    /// <returns>IEnumerable of MemberCipherDetailsResponseModel</returns>
    /// <exception cref="NotFoundException">If Access reports permission is not assigned</exception>
    [HttpGet("member-cipher-details/{orgId}")]
    public async Task<IEnumerable<MemberCipherDetailsResponseModel>> GetMemberCipherDetails(Guid orgId)
    {
        // Using the AccessReports permission here until new permissions
        // are needed for more control over reports
        if (!await _currentContext.AccessReports(orgId))
        {
            throw new NotFoundException();
        }

        var riskDetails = await GetRiskInsightsReportDetails(new RiskInsightsReportRequest { OrganizationId = orgId });

        var responses = riskDetails.Select(x => new MemberCipherDetailsResponseModel(x));

        return responses;
    }

    /// <summary>
    /// Access details for an organization member. Includes the member information,
    /// group collection assignment, and item counts
    /// </summary>
    /// <param name="orgId">Organization Id</param>
    /// <returns>IEnumerable of MemberAccessReportResponseModel</returns>
    /// <exception cref="NotFoundException">If Access reports permission is not assigned</exception>
    [HttpGet("member-access/{orgId}")]
    public async Task<IEnumerable<MemberAccessDetailReportResponseModel>> GetMemberAccessReport(Guid orgId)
    {
        if (!await _currentContext.AccessReports(orgId))
        {
            throw new NotFoundException();
        }

        var accessDetails = await GetMemberAccessDetails(new MemberAccessReportRequest { OrganizationId = orgId });

        var responses = accessDetails.Select(x => new MemberAccessDetailReportResponseModel(x));

        return responses;
    }

    /// <summary>
    /// Contains the organization member info, the cipher ids associated with the member,
    /// and details on their collections, groups, and permissions
    /// </summary>
    /// <param name="request">Request parameters</param>
    /// <returns>
    ///     List of a user's permissions at a group and collection level as well as the number of ciphers
    ///     associated with that group/collection
    /// </returns>
    private async Task<IEnumerable<MemberAccessReportDetail>> GetMemberAccessDetails(
        MemberAccessReportRequest request)
    {
        var accessDetails = await _memberAccessReportQuery.GetMemberAccessReportsAsync(request);
        return accessDetails;
    }

    /// <summary>
    /// Gets the risk insights report details from the risk insights query. Associates a user to their cipher ids
    /// </summary>
    /// <param name="request">Request parameters</param>
    /// <returns>A list of risk insights data associating the user to cipher ids</returns>
    private async Task<IEnumerable<RiskInsightsReportDetail>> GetRiskInsightsReportDetails(
        RiskInsightsReportRequest request)
    {
        var riskDetails = await _riskInsightsReportQuery.GetRiskInsightsReportDetails(request);
        return riskDetails;
    }

    /// <summary>
    /// Get the password health report applications for an organization
    /// </summary>
    /// <param name="orgId">A valid Organization Id</param>
    /// <returns>An Enumerable of PasswordHealthReportApplication </returns>
    /// <exception cref="NotFoundException">If the user lacks access</exception>
    /// <exception cref="BadRequestException">If the organization Id is not valid</exception>
    [HttpGet("password-health-report-applications/{orgId}")]
    public async Task<IEnumerable<PasswordHealthReportApplication>> GetPasswordHealthReportApplications(Guid orgId)
    {
        if (!await _currentContext.AccessReports(orgId))
        {
            throw new NotFoundException();
        }

        return await _getPwdHealthReportAppQuery.GetPasswordHealthReportApplicationAsync(orgId);
    }

    /// <summary>
    /// Adds a new record into PasswordHealthReportApplication
    /// </summary>
    /// <param name="request">A single instance of PasswordHealthReportApplication Model</param>
    /// <returns>A single instance of PasswordHealthReportApplication</returns>
    /// <exception cref="BadRequestException">If the organization Id is not valid</exception>
    /// <exception cref="NotFoundException">If the user lacks access</exception>
    [HttpPost("password-health-report-application")]
    public async Task<PasswordHealthReportApplication> AddPasswordHealthReportApplication(
        [FromBody] PasswordHealthReportApplicationModel request)
    {
        if (!await _currentContext.AccessReports(request.OrganizationId))
        {
            throw new NotFoundException();
        }

        var commandRequest = new AddPasswordHealthReportApplicationRequest
        {
            OrganizationId = request.OrganizationId,
            Url = request.Url
        };

        return await _addPwdHealthReportAppCommand.AddPasswordHealthReportApplicationAsync(commandRequest);
    }

    /// <summary>
    /// Adds multiple records into PasswordHealthReportApplication
    /// </summary>
    /// <param name="request">A enumerable of PasswordHealthReportApplicationModel</param>
    /// <returns>An Enumerable of PasswordHealthReportApplication</returns>
    /// <exception cref="NotFoundException">If user does not have access to the OrganizationId</exception>
    /// <exception cref="BadRequestException">If the organization Id is not valid</exception>
    [HttpPost("password-health-report-applications")]
    public async Task<IEnumerable<PasswordHealthReportApplication>> AddPasswordHealthReportApplications(
        [FromBody] IEnumerable<PasswordHealthReportApplicationModel> request)
    {
        if (request.Any(_ => _currentContext.AccessReports(_.OrganizationId).Result == false))
        {
            throw new NotFoundException();
        }

        var commandRequests = request.Select(request => new AddPasswordHealthReportApplicationRequest
        {
            OrganizationId = request.OrganizationId,
            Url = request.Url
        }).ToList();

        return await _addPwdHealthReportAppCommand.AddPasswordHealthReportApplicationAsync(commandRequests);
    }

    /// <summary>
    /// Drops a record from PasswordHealthReportApplication
    /// </summary>
    /// <param name="request">
    ///     A single instance of DropPasswordHealthReportApplicationRequest
    ///     { OrganizationId, array of PasswordHealthReportApplicationIds }
    /// </param>
    /// <returns></returns>
    /// <exception cref="NotFoundException">If user does not have access to the organization</exception>
    /// <exception cref="BadRequestException">If the organization does not have any records</exception>
    [HttpDelete("password-health-report-application")]
    public async Task DropPasswordHealthReportApplication(
        [FromBody] DropPasswordHealthReportApplicationRequest request)
    {
        if (!await _currentContext.AccessReports(request.OrganizationId))
        {
            throw new NotFoundException();
        }

        await _dropPwdHealthReportAppCommand.DropPasswordHealthReportApplicationAsync(request);
    }

    /// <summary>
    /// Adds a new organization report
    /// </summary>
    /// <param name="request">A single instance of AddOrganizationReportRequest</param>
    /// <returns>A single instance of OrganizationReport</returns>
    /// <exception cref="NotFoundException">If user does not have access to the organization</exception>
    /// <exception cref="BadRequestException">If the organization Id is not valid</exception>
    [HttpPost("organization-reports")]
    public async Task<OrganizationReport> AddOrganizationReport([FromBody] AddOrganizationReportRequest request)
    {
        if (!await _currentContext.AccessReports(request.OrganizationId))
        {
            throw new NotFoundException();
        }
        return await _addOrganizationReportCommand.AddOrganizationReportAsync(request);
    }

    /// <summary>
    /// Drops organization reports for an organization
    /// </summary>
    /// <param name="request">A single instance of DropOrganizationReportRequest</param>
    /// <returns></returns>
    /// <exception cref="NotFoundException">If user does not have access to the organization</exception>
    /// <exception cref="BadRequestException">If the organization does not have any records</exception>
    [HttpDelete("organization-reports")]
    public async Task DropOrganizationReport([FromBody] DropOrganizationReportRequest request)
    {
        if (!await _currentContext.AccessReports(request.OrganizationId))
        {
            throw new NotFoundException();
        }
        await _dropOrganizationReportCommand.DropOrganizationReportAsync(request);
    }

    /// <summary>
    /// Gets organization reports for an organization
    /// </summary>
    /// <param name="orgId">A valid Organization Id</param>
    /// <returns>An Enumerable of OrganizationReport</returns>
    /// <exception cref="NotFoundException">If user does not have access to the organization</exception>
    /// <exception cref="BadRequestException">If the organization Id is not valid</exception>
    [HttpGet("organization-reports/{orgId}")]
    public async Task<IEnumerable<OrganizationReport>> GetOrganizationReports(Guid orgId)
    {
        if (!await _currentContext.AccessReports(orgId))
        {
            throw new NotFoundException();
        }
        return await _getOrganizationReportQuery.GetOrganizationReportAsync(orgId);
    }

    /// <summary>
    /// Gets the latest organization report for an organization
    /// </summary>
    /// <param name="orgId">A valid Organization Id</param>
    /// <returns>A single instance of OrganizationReport</returns>
    /// <exception cref="NotFoundException">If user does not have access to the organization</exception>
    /// <exception cref="BadRequestException">If the organization Id is not valid</exception>
    [HttpGet("organization-reports/latest/{orgId}")]
    public async Task<OrganizationReport> GetLatestOrganizationReport(Guid orgId)
    {
        if (!await _currentContext.AccessReports(orgId))
        {
            throw new NotFoundException();
        }
        return await _getOrganizationReportQuery.GetLatestOrganizationReportAsync(orgId);
    }

    /// <summary>
    /// Gets the Organization Report Summary for an organization.
    /// This includes the latest report's encrypted data, encryption key, and date.
    /// This is a mock implementation and should be replaced with actual data retrieval logic.
    /// </summary>
    /// <param name="orgId"></param>
    /// <param name="from">Min date (example: 2023-01-01)</param>
    /// <param name="to">Max date (example: 2023-12-31)</param>
    /// <returns></returns>
    /// <exception cref="NotFoundException"></exception>
    [HttpGet("organization-report-summary/{orgId}")]
    public IEnumerable<OrganizationReportSummaryModel> GetOrganizationReportSummary(
        [FromRoute] Guid orgId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to)
    {
        if (!ModelState.IsValid)
        {
            throw new BadRequestException(ModelState);
        }

        GuardOrganizationAccess(orgId);

        // FIXME: remove this mock class when actual data retrieval is implemented
        return MockOrganizationReportSummary.GetMockData()
            .Where(_ => _.OrganizationId == orgId
                && _.Date >= from.ToDateTime(TimeOnly.MinValue)
                && _.Date <= to.ToDateTime(TimeOnly.MaxValue));
    }


    /// <summary>
    /// Creates a new Organization Report Summary for an organization.
    /// This is a mock implementation and should be replaced with actual creation logic.
    /// </summary>
    /// <param name="model"></param>
    /// <returns>Returns 204 Created with the created OrganizationReportSummaryModel</returns>
    /// <exception cref="NotFoundException"></exception>
    [HttpPost("organization-report-summary")]
    public IActionResult CreateOrganizationReportSummary([FromBody] OrganizationReportSummaryModel model)
    {
        if (!ModelState.IsValid)
        {
            throw new BadRequestException(ModelState);
        }

        GuardOrganizationAccess(model.OrganizationId);

        // TODO: Implement actual creation logic

        // Returns 204 No Content as a placeholder
        return NoContent();
    }

    [HttpPut("organization-report-summary")]
    public IActionResult UpdateOrganizationReportSummary([FromBody] OrganizationReportSummaryModel model)
    {
        if (!ModelState.IsValid)
        {
            throw new BadRequestException(ModelState);
        }

        GuardOrganizationAccess(model.OrganizationId);

        // TODO: Implement actual update logic

        // Returns 204 No Content as a placeholder
        return NoContent();
    }

    private void GuardOrganizationAccess(Guid organizationId)
    {
        if (!_currentContext.AccessReports(organizationId).Result)
        {
            throw new NotFoundException();
        }
    }
}
