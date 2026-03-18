// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Api.AdminConsole.Authorization;
using Bit.Api.AdminConsole.Authorization.Requirements;
using Bit.Api.AdminConsole.Models.Request;
using Bit.Api.AdminConsole.Models.Response.Helpers;
using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Api.Models.Response;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyUpdateEvents.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tokens;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.AdminConsole.Controllers;

[Route("organizations/{orgId}/policies")]
[Authorize("Application")]
public class PoliciesController : Controller
{
    private readonly ICurrentContext _currentContext;
    private readonly IOrganizationHasVerifiedDomainsQuery _organizationHasVerifiedDomainsQuery;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IDataProtectorTokenFactory<OrgUserInviteTokenable> _orgUserInviteTokenDataFactory;
    private readonly IPolicyRepository _policyRepository;
    private readonly IUserService _userService;
    private readonly ISavePolicyCommand _savePolicyCommand;
    private readonly IVNextSavePolicyCommand _vNextSavePolicyCommand;
    private readonly IPolicyQuery _policyQuery;

    public PoliciesController(IPolicyRepository policyRepository,
        IOrganizationUserRepository organizationUserRepository,
        IUserService userService,
        ICurrentContext currentContext,
        IDataProtectorTokenFactory<OrgUserInviteTokenable> orgUserInviteTokenDataFactory,
        IOrganizationHasVerifiedDomainsQuery organizationHasVerifiedDomainsQuery,
        IOrganizationRepository organizationRepository,
        ISavePolicyCommand savePolicyCommand,
        IVNextSavePolicyCommand vNextSavePolicyCommand,
        IPolicyQuery policyQuery)
    {
        _policyRepository = policyRepository;
        _organizationUserRepository = organizationUserRepository;
        _userService = userService;
        _currentContext = currentContext;
        _organizationRepository = organizationRepository;
        _orgUserInviteTokenDataFactory = orgUserInviteTokenDataFactory;
        _organizationHasVerifiedDomainsQuery = organizationHasVerifiedDomainsQuery;
        _savePolicyCommand = savePolicyCommand;
        _vNextSavePolicyCommand = vNextSavePolicyCommand;
        _policyQuery = policyQuery;
    }

    [HttpGet("{type}")]
    public async Task<PolicyStatusResponseModel> Get(Guid orgId, PolicyType type)
    {
        if (!await _currentContext.ManagePolicies(orgId))
        {
            throw new NotFoundException();
        }

        var policy = await _policyQuery.RunAsync(orgId, type);
        if (policy.Type is PolicyType.SingleOrg)
        {
            return await policy.GetSingleOrgPolicyStatusResponseAsync(_organizationHasVerifiedDomainsQuery);
        }

        return new PolicyStatusResponseModel(policy);
    }

    [HttpGet("")]
    public async Task<ListResponseModel<PolicyResponseModel>> GetAll(string orgId)
    {
        var orgIdGuid = new Guid(orgId);
        if (!await _currentContext.ManagePolicies(orgIdGuid))
        {
            throw new NotFoundException();
        }

        var policies = await _policyRepository.GetManyByOrganizationIdAsync(orgIdGuid);

        // Once migration from legacy Send policies > SendControls has run, replace the rest of this method with:
        // return new ListResponseModel<PolicyResponseModel>(policies.Select(p => new PolicyResponseModel(p)));
        var responses = policies.Select(p => new PolicyResponseModel(p)).ToList();

        if (policies.Any(p => p.Type == PolicyType.SendControls))
        {
            return new ListResponseModel<PolicyResponseModel>(responses);
        }

        var sendControlsStatus = await _policyQuery.RunAsync(orgIdGuid, PolicyType.SendControls);
        if (!sendControlsStatus.Enabled)
        {
            return new ListResponseModel<PolicyResponseModel>(responses);
        }

        var data = sendControlsStatus.GetDataModel<SendControlsPolicyData>();
        responses.Add(new PolicyResponseModel
        {
            OrganizationId = sendControlsStatus.OrganizationId,
            Type = sendControlsStatus.Type,
            Enabled = sendControlsStatus.Enabled,
            Data = new Dictionary<string, object>
            {
                ["disableSend"] = data.DisableSend,
                ["disableHideEmail"] = data.DisableHideEmail,
            },
        });

        return new ListResponseModel<PolicyResponseModel>(responses);
    }

    [AllowAnonymous]
    [HttpGet("token")]
    public async Task<ListResponseModel<PolicyResponseModel>> GetByToken(Guid orgId, [FromQuery] string email,
        [FromQuery] string token, [FromQuery] Guid organizationUserId)
    {
        var organization = await _organizationRepository.GetByIdAsync(orgId);

        if (organization is not { UsePolicies: true })
        {
            throw new NotFoundException();
        }

        var tokenValid = OrgUserInviteTokenable.ValidateOrgUserInviteStringToken(
            _orgUserInviteTokenDataFactory, token, organizationUserId, email);

        if (!tokenValid)
        {
            throw new NotFoundException();
        }

        var orgUser = await _organizationUserRepository.GetByIdAsync(organizationUserId);
        if (orgUser == null || orgUser.OrganizationId != orgId)
        {
            throw new NotFoundException();
        }

        var policies = await _policyRepository.GetManyByOrganizationIdAsync(orgId);
        var responses = policies.Where(p => p.Enabled).Select(p => new PolicyResponseModel(p));
        return new ListResponseModel<PolicyResponseModel>(responses);
    }

    // TODO: PM-4097 - remove GetByInvitedUser once all clients are updated to use the GetMasterPasswordPolicy endpoint below
    [Obsolete("Deprecated API", false)]
    [AllowAnonymous]
    [HttpGet("invited-user")]
    public async Task<ListResponseModel<PolicyResponseModel>> GetByInvitedUser(Guid orgId, [FromQuery] Guid userId)
    {
        var user = await _userService.GetUserByIdAsync(userId);
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
        var organization = await _organizationRepository.GetByIdAsync(orgId);

        if (organization is not { UsePolicies: true })
        {
            throw new NotFoundException();
        }

        var userId = _userService.GetProperUserId(User).Value;

        var orgUser = await _organizationUserRepository.GetByOrganizationAsync(orgId, userId);

        if (orgUser == null)
        {
            throw new NotFoundException();
        }

        var policy = await _policyRepository.GetByOrganizationIdTypeAsync(orgId, PolicyType.MasterPassword);

        if (policy == null || !policy.Enabled)
        {
            throw new NotFoundException();
        }

        return new PolicyResponseModel(policy);
    }

    [HttpPut("{type}")]
    [Authorize<ManagePoliciesRequirement>]
    public async Task<PolicyResponseModel> Put(Guid orgId, PolicyType type, [FromBody] PolicyRequestModel model)
    {
        return await PutVNext(orgId, type, new SavePolicyRequest { Policy = model });
    }

    [HttpPut("{type}/vnext")]
    [Authorize<ManagePoliciesRequirement>]
    public async Task<PolicyResponseModel> PutVNext(Guid orgId, PolicyType type, [FromBody] SavePolicyRequest model)
    {
        var savePolicyRequest = await model.ToSavePolicyModelAsync(orgId, type, _currentContext);

        var policy = await _vNextSavePolicyCommand.SaveAsync(savePolicyRequest);

        return new PolicyResponseModel(policy);
    }
}
