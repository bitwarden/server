using Bit.Api.Dirt.Models;
using Bit.Api.Dirt.Models.Response;
using Bit.Api.Tools.Models.Response;
using Bit.Core;
using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.Context;
using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Reports.Models.Data;
using Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;
using Bit.Core.Dirt.Reports.ReportFeatures.OrganizationReportMembers.Interfaces;
using Bit.Core.Dirt.Reports.ReportFeatures.Requests;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Utilities;
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
    private readonly IGetPasskeyDirectoryQuery _getPasskeyDirectoryQuery;
    private readonly IOrganizationAbilityCacheService _orgAbilityCacheService;
    private readonly ILogger<ReportsController> _logger;

    public ReportsController(
        ICurrentContext currentContext,
        IMemberAccessReportQuery memberAccessReportQuery,
        IRiskInsightsReportQuery riskInsightsReportQuery,
        IAddPasswordHealthReportApplicationCommand addPasswordHealthReportApplicationCommand,
        IGetPasswordHealthReportApplicationQuery getPasswordHealthReportApplicationQuery,
        IDropPasswordHealthReportApplicationCommand dropPwdHealthReportAppCommand,
        IGetPasskeyDirectoryQuery getPasskeyDirectoryQuery,
        IOrganizationAbilityCacheService orgAbilityCacheService,
        ILogger<ReportsController> logger
    )
    {
        _currentContext = currentContext;
        _memberAccessReportQuery = memberAccessReportQuery;
        _riskInsightsReportQuery = riskInsightsReportQuery;
        _addPwdHealthReportAppCommand = addPasswordHealthReportApplicationCommand;
        _getPwdHealthReportAppQuery = getPasswordHealthReportApplicationQuery;
        _dropPwdHealthReportAppCommand = dropPwdHealthReportAppCommand;
        _getPasskeyDirectoryQuery = getPasskeyDirectoryQuery;
        _orgAbilityCacheService = orgAbilityCacheService;
        _logger = logger;
    }

    /// <summary>
    /// Organization member information containing a list of cipher ids
    /// assigned
    /// </summary>
    /// <param name="orgId">Organzation Id</param>
    /// <returns>IEnumerable of MemberCipherDetailsResponseModel</returns>
    /// <exception cref="NotFoundException">If Access reports permission is not assigned</exception>
    [HttpGet("member-cipher-details/{orgId}")]
    [RequireOrganizationAbility(nameof(OrganizationAbility.UseRiskInsights))]
    public async Task<IEnumerable<MemberCipherDetailsResponseModel>> GetMemberCipherDetails(Guid orgId)
    {
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
    [RequireOrganizationAbility(nameof(OrganizationAbility.UseRiskInsights))]
    public async Task<IEnumerable<MemberAccessDetailReportResponseModel>> GetMemberAccessReport(Guid orgId)
    {
        if (!await _currentContext.AccessReports(orgId))
        {
            _logger.LogInformation(Constants.BypassFiltersEventId,
                "AccessReports Check - UserId: {userId} OrgId: {orgId} DeviceType: {deviceType}",
                _currentContext.UserId, orgId, _currentContext.DeviceType);
            throw new NotFoundException();
        }

        _logger.LogInformation(Constants.BypassFiltersEventId,
            "MemberAccessReportQuery starts - UserId: {userId} OrgId: {orgId} DeviceType: {deviceType}",
            _currentContext.UserId, orgId, _currentContext.DeviceType);

        var accessDetails = await _memberAccessReportQuery
            .GetMemberAccessReportsAsync(new MemberAccessReportRequest { OrganizationId = orgId });

        var responses = accessDetails.Select(x => new MemberAccessDetailReportResponseModel(x));

        return responses;
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
    [RequireOrganizationAbility(nameof(OrganizationAbility.UseRiskInsights))]
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
        await AuthorizeAsync(request.OrganizationId);

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
        foreach (var item in request)
        {
            await AuthorizeAsync(item.OrganizationId);
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
        await AuthorizeAsync(request.OrganizationId);

        await _dropPwdHealthReportAppCommand.DropPasswordHealthReportApplicationAsync(request);
    }

    /// <summary>
    /// Gets the list of domains that support passkeys from the 2FA Directory
    /// </summary>
    /// <returns>List of domains with passkey support details</returns>
    [HttpGet("passkey-directory")]
    [RequireFeature(FeatureFlagKeys.PasskeyDirectoryReport)]
    public async Task<IEnumerable<PasskeyDirectoryResponseModel>> GetPasskeyDirectoryAsync()
    {
        var entries = await _getPasskeyDirectoryQuery.GetPasskeyDirectoryAsync();
        return entries.Select(e => new PasskeyDirectoryResponseModel
        {
            DomainName = e.DomainName,
            Passwordless = e.Passwordless,
            Mfa = e.Mfa,
            Instructions = e.Instructions
        });
    }

    /// <summary>
    /// Verifies the current Organization is authorized to access the Access Intelligence (formerly Risk Insights) reporting feature.
    /// </summary>
    /// <param name="organizationId">The organization ID to authorize.</param>
    private async Task AuthorizeAsync(Guid organizationId)
    {
        if (!await _currentContext.AccessReports(organizationId))
        {
            throw new NotFoundException();
        }

        // still required since the RequireOrganizationAbilityAttribute can not be applied to all endpoints in this controller - the organizationId is not present in route. 
        var orgAbility = await _orgAbilityCacheService.GetOrganizationAbilityAsync(organizationId);
        if (orgAbility == null || !orgAbility.UseRiskInsights)
        {
            throw new NotFoundException("The user's organization does not have access to this feature in their plan.");
        }
    }
}
