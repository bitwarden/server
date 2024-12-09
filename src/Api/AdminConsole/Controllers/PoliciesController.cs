using Bit.Api.AdminConsole.Models.Request;
using Bit.Api.AdminConsole.Models.Response.Helpers;
using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Api.Models.Response;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Tokens;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.AdminConsole.Controllers;

[Route("organizations/{orgId}/policies")]
[Authorize("Application")]
public class PoliciesController : Controller
{
    private readonly IPolicyRepository _policyRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IUserService _userService;
    private readonly ICurrentContext _currentContext;
    private readonly GlobalSettings _globalSettings;
    private readonly IDataProtector _organizationServiceDataProtector;
    private readonly IDataProtectorTokenFactory<OrgUserInviteTokenable> _orgUserInviteTokenDataFactory;
    private readonly IFeatureService _featureService;
    private readonly IOrganizationHasVerifiedDomainsQuery _organizationHasVerifiedDomainsQuery;
    private readonly ISavePolicyCommand _savePolicyCommand;

    public PoliciesController(
        IPolicyRepository policyRepository,
        IOrganizationUserRepository organizationUserRepository,
        IUserService userService,
        ICurrentContext currentContext,
        GlobalSettings globalSettings,
        IDataProtectionProvider dataProtectionProvider,
        IDataProtectorTokenFactory<OrgUserInviteTokenable> orgUserInviteTokenDataFactory,
        IFeatureService featureService,
        IOrganizationHasVerifiedDomainsQuery organizationHasVerifiedDomainsQuery,
        ISavePolicyCommand savePolicyCommand)
    {
        _policyRepository = policyRepository;
        _organizationUserRepository = organizationUserRepository;
        _userService = userService;
        _currentContext = currentContext;
        _globalSettings = globalSettings;
        _organizationServiceDataProtector = dataProtectionProvider.CreateProtector(
            "OrganizationServiceDataProtector");

        _orgUserInviteTokenDataFactory = orgUserInviteTokenDataFactory;
        _featureService = featureService;
        _organizationHasVerifiedDomainsQuery = organizationHasVerifiedDomainsQuery;
        _savePolicyCommand = savePolicyCommand;
    }

    [HttpGet("{type}")]
    public async Task<PolicyDetailResponseModel> Get(Guid orgId, int type)
    {
        if (!await _currentContext.ManagePolicies(orgId))
        {
            throw new NotFoundException();
        }
        var policy = await _policyRepository.GetByOrganizationIdTypeAsync(orgId, (PolicyType)type);
        if (policy == null)
        {
            return new PolicyDetailResponseModel(new Policy { Type = (PolicyType)type });
        }

        if (_featureService.IsEnabled(FeatureFlagKeys.AccountDeprovisioning) && policy.Type is PolicyType.SingleOrg)
        {
            return await policy.GetSingleOrgPolicyDetailResponseAsync(_organizationHasVerifiedDomainsQuery);
        }

        return new PolicyDetailResponseModel(policy);
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

        return new ListResponseModel<PolicyResponseModel>(policies.Select(p => new PolicyResponseModel(p)));
    }

    [AllowAnonymous]
    [HttpGet("token")]
    public async Task<ListResponseModel<PolicyResponseModel>> GetByToken(Guid orgId, [FromQuery] string email,
        [FromQuery] string token, [FromQuery] Guid organizationUserId)
    {
        // TODO: PM-4142 - remove old token validation logic once 3 releases of backwards compatibility are complete
        var newTokenValid = OrgUserInviteTokenable.ValidateOrgUserInviteStringToken(
            _orgUserInviteTokenDataFactory, token, organizationUserId, email);

        var tokenValid = newTokenValid || CoreHelpers.UserInviteTokenIsValid(
            _organizationServiceDataProtector, token, email, organizationUserId, _globalSettings
        );

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
    public async Task<PolicyResponseModel> Put(Guid orgId, PolicyType type, [FromBody] PolicyRequestModel model)
    {
        if (!await _currentContext.ManagePolicies(orgId))
        {
            throw new NotFoundException();
        }

        if (type != model.Type)
        {
            throw new BadRequestException("Mismatched policy type");
        }

        var policyUpdate = await model.ToPolicyUpdateAsync(orgId, _currentContext);
        var policy = await _savePolicyCommand.SaveAsync(policyUpdate);
        return new PolicyResponseModel(policy);
    }
}
