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

namespace Bit.Api.Controllers
{
    [Route("organizations/{orgId}/policies")]
    [Authorize("Application")]
    public class PoliciesController : Controller
    {
        private readonly IPolicyRepository _policyRepository;
        private readonly CurrentContext _currentContext;

        public PoliciesController(
            IPolicyRepository policyRepository,
            CurrentContext currentContext)
        {
            _policyRepository = policyRepository;
            _currentContext = currentContext;
        }

        [HttpGet("{id}")]
        public async Task<PolicyResponseModel> Get(string orgId, string id)
        {
            var policy = await _policyRepository.GetByIdAsync(new Guid(id));
            if(policy == null || !_currentContext.OrganizationAdmin(policy.OrganizationId))
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

        [HttpPost("")]
        public async Task<PolicyResponseModel> Post(string orgId, [FromBody]PolicyRequestModel model)
        {
            var orgIdGuid = new Guid(orgId);
            if(!_currentContext.OrganizationAdmin(orgIdGuid))
            {
                throw new NotFoundException();
            }

            var policy = model.ToPolicy(orgIdGuid);
            //await _groupService.SaveAsync(group, model.Collections?.Select(c => c.ToSelectionReadOnly()));
            return new PolicyResponseModel(policy);
        }

        [HttpPut("{id}")]
        [HttpPost("{id}")]
        public async Task<PolicyResponseModel> Put(string orgId, string id, [FromBody]PolicyRequestModel model)
        {
            var policy = await _policyRepository.GetByIdAsync(new Guid(id));
            if(policy == null || !_currentContext.OrganizationAdmin(policy.OrganizationId))
            {
                throw new NotFoundException();
            }

            //await _groupService.SaveAsync(model.ToPolicy(policy));
            return new PolicyResponseModel(policy);
        }

        [HttpDelete("{id}")]
        [HttpPost("{id}/delete")]
        public async Task Delete(string orgId, string id)
        {
            var policy = await _policyRepository.GetByIdAsync(new Guid(id));
            if(policy == null || !_currentContext.OrganizationAdmin(policy.OrganizationId))
            {
                throw new NotFoundException();
            }

            //await _groupService.DeleteAsync(policy);
        }
    }
}
