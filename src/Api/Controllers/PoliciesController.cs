using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Authorization;
using Bit.Core.Models.Api;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Utilities;
using Bit.Core.Settings;
using Microsoft.AspNetCore.DataProtection;

namespace Bit.Api.Controllers
{
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
        public async Task<ListResponseModel<PolicyResponseModel>> GetByToken(string orgId, [FromQuery]string email,
            [FromQuery]string token, [FromQuery]string organizationUserId)
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

        [HttpPut("{type}")]
        public async Task<PolicyResponseModel> Put(string orgId, int type, [FromBody]PolicyRequestModel model)
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
}
