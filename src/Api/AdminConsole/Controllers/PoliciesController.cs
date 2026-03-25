// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Api.AdminConsole.Authorization;
using Bit.Api.AdminConsole.Authorization.Requirements;
using Bit.Api.AdminConsole.Models.Request;
using Bit.Api.AdminConsole.Models.Response.Helpers;
using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Api.Models.Response;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyUpdateEvents.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
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
    private readonly IVNextSavePolicyCommand _vNextSavePolicyCommand;
    private readonly IPolicyQuery _policyQuery;

    public PoliciesController(IPolicyRepository policyRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICurrentContext currentContext,
        IDataProtectorTokenFactory<OrgUserInviteTokenable> orgUserInviteTokenDataFactory,
        IOrganizationHasVerifiedDomainsQuery organizationHasVerifiedDomainsQuery,
        IOrganizationRepository organizationRepository,
        IVNextSavePolicyCommand vNextSavePolicyCommand,
        IPolicyQuery policyQuery)
    {
        _policyRepository = policyRepository;
        _organizationUserRepository = organizationUserRepository;
        _currentContext = currentContext;
        _organizationRepository = organizationRepository;
        _orgUserInviteTokenDataFactory = orgUserInviteTokenDataFactory;
        _organizationHasVerifiedDomainsQuery = organizationHasVerifiedDomainsQuery;
        _vNextSavePolicyCommand = vNextSavePolicyCommand;
        _policyQuery = policyQuery;
    }

    [HttpGet("{type}")]
    [Authorize<ManagePoliciesRequirement>]
    public async Task<PolicyStatusResponseModel> Get(Guid orgId, PolicyType type)
    {
        var policy = await _policyQuery.RunAsync(orgId, type);
        if (policy.Type is PolicyType.SingleOrg)
        {
            return await policy.GetSingleOrgPolicyStatusResponseAsync(_organizationHasVerifiedDomainsQuery);
        }

        return new PolicyStatusResponseModel(policy);
    }

    [HttpGet("")]
    [Authorize<ManagePoliciesRequirement>]
    public async Task<ListResponseModel<PolicyResponseModel>> GetAll(Guid orgId)
    {
        var policies = await _policyRepository.GetManyByOrganizationIdAsync(orgId);

        return new ListResponseModel<PolicyResponseModel>(policies.Select(p => new PolicyResponseModel(p)));
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

    [HttpGet("master-password")]
    [Authorize<OrgUserLinkedToUserIdRequirement>]
    public async Task<PolicyResponseModel> GetMasterPasswordPolicy(Guid orgId)
    {
        var organization = await _organizationRepository.GetByIdAsync(orgId);

        if (organization is not { UsePolicies: true })
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
