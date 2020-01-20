using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Authorization;
using Bit.Core.Models.Api;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Core;
using Bit.Core.Enums;

namespace Bit.Api.Controllers
{
    [Route("organizations/{orgId}/policies")]
    [Authorize("Application")]
    public class PoliciesController : Controller
    {
        private readonly IPolicyRepository _policyRepository;
        private readonly IPolicyService _policyService;
        private readonly CurrentContext _currentContext;

        public PoliciesController(
            IPolicyRepository policyRepository,
            IPolicyService policyService,
            CurrentContext currentContext)
        {
            _policyRepository = policyRepository;
            _policyService = policyService;
            _currentContext = currentContext;
        }

        [HttpGet("{type}")]
        public async Task<PolicyResponseModel> Get(string orgId, int type)
        {
            var orgIdGuid = new Guid(orgId);
            if(!_currentContext.OrganizationAdmin(orgIdGuid))
            {
                throw new NotFoundException();
            }
            var policy = await _policyRepository.GetByOrganizationIdTypeAsync(orgIdGuid, (PolicyType)type);
            if(policy == null)
            {
                throw new NotFoundException();
            }

            return new PolicyResponseModel(policy);
        }

        [HttpGet("")]
        public async Task<ListResponseModel<PolicyResponseModel>> Get(string orgId)
        {
            var orgIdGuid = new Guid(orgId);
            if(!_currentContext.OrganizationManager(orgIdGuid))
            {
                throw new NotFoundException();
            }

            var policies = await _policyRepository.GetManyByOrganizationIdAsync(orgIdGuid);
            var responses = policies.Select(p => new PolicyResponseModel(p));
            return new ListResponseModel<PolicyResponseModel>(responses);
        }

        [HttpPut("{type}")]
        public async Task<PolicyResponseModel> Put(string orgId, int type, [FromBody]PolicyRequestModel model)
        {
            var orgIdGuid = new Guid(orgId);
            if(!_currentContext.OrganizationAdmin(orgIdGuid))
            {
                throw new NotFoundException();
            }
            var policy = await _policyRepository.GetByOrganizationIdTypeAsync(new Guid(orgId), (PolicyType)type);
            if(policy == null)
            {
                policy = model.ToPolicy(orgIdGuid);
            }
            else
            {
                policy = model.ToPolicy(policy);
            }

            await _policyService.SaveAsync(policy);
            return new PolicyResponseModel(policy);
        }
    }
}
