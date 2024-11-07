using Bit.Api.Tools.Models;
using Bit.Api.Tools.Models.Response;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.ReportFeatures.Interfaces;
using Bit.Core.Tools.Requests;
using Bit.Core.Vault.Queries;
using Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Tools.Controllers;

[Route("reports")]
[Authorize("Application")]
public class ReportsController : Controller
{
    private readonly IOrganizationUserUserDetailsQuery _organizationUserUserDetailsQuery;
    private readonly IGroupRepository _groupRepository;
    private readonly ICollectionRepository _collectionRepository;
    private readonly ICurrentContext _currentContext;
    private readonly IOrganizationCiphersQuery _organizationCiphersQuery;
    private readonly IApplicationCacheService _applicationCacheService;
    private readonly ITwoFactorIsEnabledQuery _twoFactorIsEnabledQuery;
    private readonly IAddPasswordHealthReportApplicationCommand _addPwdHealthReportAppCommand;
    private readonly IGetPasswordHealthReportApplicationQuery _getPwdHealthReportAppQuery;

    public ReportsController(
        IOrganizationUserUserDetailsQuery organizationUserUserDetailsQuery,
        IGroupRepository groupRepository,
        ICollectionRepository collectionRepository,
        ICurrentContext currentContext,
        IOrganizationCiphersQuery organizationCiphersQuery,
        IApplicationCacheService applicationCacheService,
        ITwoFactorIsEnabledQuery twoFactorIsEnabledQuery,
        IAddPasswordHealthReportApplicationCommand addPasswordHealthReportApplicationCommand,
        IGetPasswordHealthReportApplicationQuery getPasswordHealthReportApplicationQuery
    )
    {
        _organizationUserUserDetailsQuery = organizationUserUserDetailsQuery;
        _groupRepository = groupRepository;
        _collectionRepository = collectionRepository;
        _currentContext = currentContext;
        _organizationCiphersQuery = organizationCiphersQuery;
        _applicationCacheService = applicationCacheService;
        _twoFactorIsEnabledQuery = twoFactorIsEnabledQuery;
        _addPwdHealthReportAppCommand = addPasswordHealthReportApplicationCommand;
        _getPwdHealthReportAppQuery = getPasswordHealthReportApplicationQuery;
    }

    [HttpGet("member-access/{orgId}")]
    public async Task<IEnumerable<MemberAccessReportResponseModel>> GetMemberAccessReport(Guid orgId)
    {
        if (!await _currentContext.AccessReports(orgId))
        {
            throw new NotFoundException();
        }

        var orgUsers = await _organizationUserUserDetailsQuery.GetOrganizationUserUserDetails(
            new OrganizationUserUserDetailsQueryRequest
            {
                OrganizationId = orgId,
                IncludeCollections = true,
                IncludeGroups = true
            });

        var orgGroups = await _groupRepository.GetManyByOrganizationIdAsync(orgId);
        var orgAbility = await _applicationCacheService.GetOrganizationAbilityAsync(orgId);
        var orgCollectionsWithAccess = await _collectionRepository.GetManyByOrganizationIdWithAccessAsync(orgId);
        var orgItems = await _organizationCiphersQuery.GetAllOrganizationCiphers(orgId);
        var organizationUsersTwoFactorEnabled = await _twoFactorIsEnabledQuery.TwoFactorIsEnabledAsync(orgUsers);

        var reports = MemberAccessReportResponseModel.CreateReport(
            orgGroups,
            orgCollectionsWithAccess,
            orgItems,
            organizationUsersTwoFactorEnabled,
            orgAbility);
        return reports;
    }

    [HttpGet("password-health-report-applications/{orgId}")]
    public async Task<IEnumerable<PasswordHealthReportApplication>> GetPasswordHealthReportApplications(Guid orgId)
    {
        if (!await _currentContext.AccessReports(orgId))
        {
            throw new NotFoundException();
        }

        return await _getPwdHealthReportAppQuery.GetPasswordHealthReportApplicationAsync(orgId);
    }

    [HttpPost("password-health-report-application")]
    public async Task<PasswordHealthReportApplication> AddPasswordHealthReportApplication(
        [FromBody] PasswordHealthReportApplicationModel request)
    {
        var commandRequest = new AddPasswordHealthReportApplicationRequest
        {
            OrganizationId = request.OrganizationId,
            Url = request.Url
        };

        return await _addPwdHealthReportAppCommand.AddPasswordHealthReportApplicationAsync(commandRequest);
    }

    [HttpPost("password-health-report-applications")]
    public async Task<IEnumerable<PasswordHealthReportApplication>> AddPasswordHealthReportApplications(
        [FromBody] IEnumerable<PasswordHealthReportApplicationModel> request)
    {
        var commandRequests = request.Select(request => new AddPasswordHealthReportApplicationRequest
        {
            OrganizationId = request.OrganizationId,
            Url = request.Url
        }).ToList();

        return await _addPwdHealthReportAppCommand.AddPasswordHealthReportApplicationAsync(commandRequests);
    }
}
