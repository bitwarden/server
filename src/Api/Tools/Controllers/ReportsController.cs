using Bit.Api.Tools.Models;
using Bit.Api.Tools.Models.Response;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Models.Data;
using Bit.Core.Tools.ReportFeatures.Interfaces;
using Bit.Core.Tools.ReportFeatures.OrganizationReportMembers.Interfaces;
using Bit.Core.Tools.ReportFeatures.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Tools.Controllers;

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
}
