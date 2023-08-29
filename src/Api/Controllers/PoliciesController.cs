using Bit.Api.Models.Request;
using Bit.Api.Models.Response;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Api.Response;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Controllers;

[Route("organizations/{orgId}/policies")]
[Authorize("Application")]
public class PoliciesController : Controller
{
    private readonly IPolicyRepository _policyRepository;
    private readonly IPolicyService _policyService;
    private readonly IOrganizationService _organizationService;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IUserService _userService;
    private readonly ICurrentContext _currentContext;
    private readonly GlobalSettings _globalSettings;
    private readonly IDataProtector _organizationServiceDataProtector;

    public PoliciesController(
        IPolicyRepository policyRepository,
        IPolicyService policyService,
        IOrganizationService organizationService,
        IOrganizationUserRepository organizationUserRepository,
        IUserService userService,
        ICurrentContext currentContext,
        GlobalSettings globalSettings,
        IDataProtectionProvider dataProtectionProvider)
    {
        _policyRepository = policyRepository;
        _policyService = policyService;
        _organizationService = organizationService;
        _organizationUserRepository = organizationUserRepository;
        _userService = userService;
        _currentContext = currentContext;
        _globalSettings = globalSettings;
        _organizationServiceDataProtector = dataProtectionProvider.CreateProtector(
            "OrganizationServiceDataProtector");
    }

    [HttpGet("{type}")]
    public async Task<PolicyResponseModel> Get(string orgId, int type)
    {
        var orgIdGuid = new Guid(orgId);
        if (!await _currentContext.ManagePolicies(orgIdGuid))
        {
            throw new NotFoundException();
        }
        var policy = await _policyRepository.GetByOrganizationIdTypeAsync(orgIdGuid, (PolicyType)type);
        if (policy == null)
        {
            throw new NotFoundException();
        }

        return new PolicyResponseModel(policy);
    }

    [HttpGet("")]
    public async Task<ListResponseModel<PolicyResponseModel>> Get(string orgId)
    {
        var orgIdGuid = new Guid(orgId);
        if (!await _currentContext.ManagePolicies(orgIdGuid))
        {
            throw new NotFoundException();
        }

        var policies = await _policyRepository.GetManyByOrganizationIdAsync(orgIdGuid);
        var responses = policies.Select(p => new PolicyResponseModel(p));
        return new ListResponseModel<PolicyResponseModel>(responses);
    }

    [AllowAnonymous]
    [HttpGet("token")]
    public async Task<ListResponseModel<PolicyResponseModel>> GetByToken(string orgId, [FromQuery] string email,
        [FromQuery] string token, [FromQuery] string organizationUserId)
    {
        var orgUserId = new Guid(organizationUserId);
        var tokenValid = CoreHelpers.UserInviteTokenIsValid(_organizationServiceDataProtector, token,
            email, orgUserId, _globalSettings);
        if (!tokenValid)
        {
            throw new NotFoundException();
        }

        var orgIdGuid = new Guid(orgId);
        var orgUser = await _organizationUserRepository.GetByIdAsync(orgUserId);
        if (orgUser == null || orgUser.OrganizationId != orgIdGuid)
        {
            throw new NotFoundException();
        }

        var policies = await _policyRepository.GetManyByOrganizationIdAsync(orgIdGuid);
        var responses = policies.Where(p => p.Enabled).Select(p => new PolicyResponseModel(p));
        return new ListResponseModel<PolicyResponseModel>(responses);
    }

    // TODO: remove GetByInvitedUser once all clients are updated to use the GetMasterPasswordPolicy endpoint below
    // TODO: create PM-??? tech debt item to track this work. In theory, target release for removal should be 2024.01.0
    // TODO: figure out how to mark this as deprecated.  [Obsolete("Deprecated API", false)]?
    [AllowAnonymous]
    [HttpGet("invited-user")]
    public async Task<ListResponseModel<PolicyResponseModel>> GetByInvitedUser(Guid orgId, [FromQuery] string userId)
    {
        var user = await _userService.GetUserByIdAsync(new Guid(userId));
        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }
        var orgUsersByUserId = await _organizationUserRepository.GetManyByUserAsync(user.Id);
        var orgUser = orgUsersByUserId.SingleOrDefault(u => u.OrganizationId == orgId);
        if (orgUser == null)
        {
            throw new NotFoundException();
        }
        if (orgUser.Status != OrganizationUserStatusType.Invited)
        {
            throw new UnauthorizedAccessException();
        }

        var policies = await _policyRepository.GetManyByOrganizationIdAsync(orgId);
        var responses = policies.Where(p => p.Enabled).Select(p => new PolicyResponseModel(p));
        return new ListResponseModel<PolicyResponseModel>(responses);
    }

    [HttpGet("master-password")]
    public async Task<PolicyResponseModel> GetMasterPasswordPolicy(Guid orgId)
    {
        var userId = _userService.GetProperUserId(User).Value;

        var orgUsersByUserId = await _organizationUserRepository.GetManyByUserAsync(userId);
        var orgUser = orgUsersByUserId.SingleOrDefault(u => u.OrganizationId == orgId);
        if (orgUser == null)
        {
            throw new NotFoundException();
        }

        var policy = await _policyRepository.GetByOrganizationIdTypeAsync(orgId, PolicyType.MasterPassword);

        if (!policy.Enabled)
        {
            throw new NotFoundException();
        }

        return new PolicyResponseModel(policy);
    }

    [HttpPut("{type}")]
    public async Task<PolicyResponseModel> Put(string orgId, int type, [FromBody] PolicyRequestModel model)
    {
        var orgIdGuid = new Guid(orgId);
        if (!await _currentContext.ManagePolicies(orgIdGuid))
        {
            throw new NotFoundException();
        }
        var policy = await _policyRepository.GetByOrganizationIdTypeAsync(new Guid(orgId), (PolicyType)type);
        if (policy == null)
        {
            policy = model.ToPolicy(orgIdGuid);
        }
        else
        {
            policy = model.ToPolicy(policy);
        }

        var userId = _userService.GetProperUserId(User);
        await _policyService.SaveAsync(policy, _userService, _organizationService, userId);
        return new PolicyResponseModel(policy);
    }
}
