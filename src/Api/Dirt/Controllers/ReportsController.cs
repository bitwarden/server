using Bit.Api.Dirt.Models;
using Bit.Api.Dirt.Models.Response;
using Bit.Core.Context;
using Bit.Core.Dirt.Reports.Entities;
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
    private readonly IMemberAccessCipherDetailsQuery _memberAccessCipherDetailsQuery;
    private readonly IAddPasswordHealthReportApplicationCommand _addPwdHealthReportAppCommand;
    private readonly IGetPasswordHealthReportApplicationQuery _getPwdHealthReportAppQuery;
    private readonly IDropPasswordHealthReportApplicationCommand _dropPwdHealthReportAppCommand;

    public ReportsController(
        ICurrentContext currentContext,
        IMemberAccessCipherDetailsQuery memberAccessCipherDetailsQuery,
        IAddPasswordHealthReportApplicationCommand addPasswordHealthReportApplicationCommand,
        IGetPasswordHealthReportApplicationQuery getPasswordHealthReportApplicationQuery,
        IDropPasswordHealthReportApplicationCommand dropPwdHealthReportAppCommand
    )
    {
        _currentContext = currentContext;
        _memberAccessCipherDetailsQuery = memberAccessCipherDetailsQuery;
        _addPwdHealthReportAppCommand = addPasswordHealthReportApplicationCommand;
        _getPwdHealthReportAppQuery = getPasswordHealthReportApplicationQuery;
        _dropPwdHealthReportAppCommand = dropPwdHealthReportAppCommand;
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

        var memberCipherDetails = await GetMemberCipherDetails(new MemberAccessCipherDetailsRequest { OrganizationId = orgId });

        var responses = memberCipherDetails.Select(x => new MemberCipherDetailsResponseModel(x));

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
    public async Task<IEnumerable<MemberAccessReportResponseModel>> GetMemberAccessReport(Guid orgId)
    {
        if (!await _currentContext.AccessReports(orgId))
        {
            throw new NotFoundException();
        }

        var memberCipherDetails = await GetMemberCipherDetails(new MemberAccessCipherDetailsRequest { OrganizationId = orgId });

        var responses = memberCipherDetails.Select(x => new MemberAccessReportResponseModel(x));

        return responses;
    }

    /// <summary>
    /// Contains the organization member info, the cipher ids associated with the member,
    /// and details on their collections, groups, and permissions
    /// </summary>
    /// <param name="request">Request to the MemberAccessCipherDetailsQuery</param>
    /// <returns>IEnumerable of MemberAccessCipherDetails</returns>
    private async Task<IEnumerable<MemberAccessCipherDetails>> GetMemberCipherDetails(MemberAccessCipherDetailsRequest request)
    {
        var memberCipherDetails =
            await _memberAccessCipherDetailsQuery.GetMemberAccessCipherDetails(request);
        return memberCipherDetails;
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
    /// Gets the latest generated organization report (Risk Insight Report 1st tab)
    /// </summary>
    /// <param name="request">A OrganizationReportRequest with organizationId within it to pull report</param>
    /// <returns>A single instance of OrganizationReport</returns>
    /// <exception cref="BadRequestException">If the organization Id is not valid</exception>
    /// <exception cref="NotFoundException">If the user lacks access</exception>
    [HttpGet("organization-report/{orgId}")]
    public async Task<OrganizationReport> GetOrganizationReportByOrgId(OrganizationReportRequest request)
    {
        if (!await _currentContext.AccessReports(request.OrganizationId))
        {
            throw new NotFoundException();
        }

        return OrganizationReportQuery(); // Placeholder return, replace with actual implementation
    }

    /// <summary>
    /// Saves a new generated organization report (Risk Insight Report first tab)
    /// </summary>
    /// <param name="request">A single instance of OrganizationReport mocdel</param>
    /// <returns>A single instance of OrganizationReport</returns>
    /// <exception cref="BadRequestException">If the organization Id is not valid</exception>
    /// <exception cref="NotFoundException">If the user lacks access</exception>
    [HttpPost("organization-report")]
    public async Task SaveOrganizationReport([FromBody] OrganizationReport request)
    {
        if (!await _currentContext.AccessReports(model.OrganizationId))
        {
            throw new NotFoundException();
        }

        // Implementation for creating an organization report
        // This method is currently empty and needs to be implemented

        return OrganizationReportSaveCommand(); // Placeholder return, replace with actual implementation
    }

    /// <summary>
    /// Gets the latest generated organization application report (Risk Insight Report second tab)
    /// </summary>
    /// <param name="request">A OrganizationApplicationRequest with organizationId within it to pull report</param>
    /// <returns>A single instance of OrganizationApplication</returns>
    /// <exception cref="BadRequestException">If the organization Id is not valid</exception>
    /// <exception cref="NotFoundException">If the user lacks access</exception>
    [HttpGet("organization-application/{orgId}")]
    public async Task<OrganizationApplication> GetOrganizationApplicationByOrgId(OrganizationApplicationRequest request)
    {
        if (!await _currentContext.AccessReports(request.OrganizationId))
        {
            throw new NotFoundException();
        }

        // Implementation for getting organization application by orgId
        // This method is currently empty and needs to be implemented

        return OrganizationApplicationQuery(); // Placeholder return, replace with actual implementation
    }


    /// <summary>
    /// Saves a new generated organization application report (Risk Insight Report)
    /// </summary>
    /// <param name="request">A single instance of OrganizationApplication model</param>
    /// <returns>A single instance of OrganizationApplication</returns>
    /// <exception cref="BadRequestException">If the organization Id is not valid</exception>
    /// <exception cref="NotFoundException">If the user lacks access</exception>
    [HttpPost("organization-application")]
    public async Task SaveOrganizationApplication([FromBody] OrganizationApplication request)
    {
        if (!await _currentContext.AccessReports(request.OrganizationId))
        {
            throw new NotFoundException();
        }

        // Implementation for creating an organization application
        // This method is currently empty and needs to be implemented

        return OrganizationApplicationSaveCommand(); // Placeholder return, replace with actual implementation
    }
}
