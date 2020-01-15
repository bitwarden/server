using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Bit.Core;
using Bit.Core.Models.Api.Public;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Public.Controllers
{
    [Route("public/policies")]
    [Authorize("Organization")]
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

        /// <summary>
        /// Retrieve a policy.
        /// </summary>
        /// <remarks>
        /// Retrieves the details of an existing policy. You need only supply the unique group identifier
        /// that was returned upon policy creation.
        /// </remarks>
        /// <param name="id">The identifier of the policy to be retrieved.</param>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(GroupResponseModel), (int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> Get(Guid id)
        {
            var policy = await _policyRepository.GetByIdAsync(id);
            if(policy == null || policy.OrganizationId != _currentContext.OrganizationId)
            {
                return new NotFoundResult();
            }
            var response = new PolicyResponseModel(policy);
            return new JsonResult(response);
        }

        /// <summary>
        /// List all policies.
        /// </summary>
        /// <remarks>
        /// Returns a list of your organization's policies.
        /// </remarks>
        [HttpGet]
        [ProducesResponseType(typeof(ListResponseModel<PolicyResponseModel>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> List()
        {
            var policies = await _policyRepository.GetManyByOrganizationIdAsync(_currentContext.OrganizationId.Value);
            var policyResponses = policies.Select(p => new PolicyResponseModel(p));
            var response = new ListResponseModel<PolicyResponseModel>(policyResponses);
            return new JsonResult(response);
        }

        /// <summary>
        /// Create a policy.
        /// </summary>
        /// <remarks>
        /// Creates a new policy object.
        /// </remarks>
        /// <param name="model">The request model.</param>
        [HttpPost]
        [ProducesResponseType(typeof(PolicyResponseModel), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ErrorResponseModel), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> Post([FromBody]PolicyCreateRequestModel model)
        {
            var policy = model.ToPolicy(_currentContext.OrganizationId.Value);
            await _policyService.SaveAsync(policy);
            var response = new PolicyResponseModel(policy);
            return new JsonResult(response);
        }

        /// <summary>
        /// Update a policy.
        /// </summary>
        /// <remarks>
        /// Updates the specified policy object. If a property is not provided,
        /// the value of the existing property will be reset.
        /// </remarks>
        /// <param name="id">The identifier of the policy to be updated.</param>
        /// <param name="model">The request model.</param>
        [HttpPut("{id}")]
        [ProducesResponseType(typeof(PolicyResponseModel), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ErrorResponseModel), (int)HttpStatusCode.BadRequest)]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> Put(Guid id, [FromBody]PolicyUpdateRequestModel model)
        {
            var existingPolicy = await _policyRepository.GetByIdAsync(id);
            if(existingPolicy == null || existingPolicy.OrganizationId != _currentContext.OrganizationId)
            {
                return new NotFoundResult();
            }
            var updatedPolicy = model.ToPolicy(existingPolicy);
            await _policyService.SaveAsync(updatedPolicy);
            var response = new PolicyResponseModel(updatedPolicy);
            return new JsonResult(response);
        }

        /// <summary>
        /// Delete a policy.
        /// </summary>
        /// <remarks>
        /// Permanently deletes a policy. This cannot be undone.
        /// </remarks>
        /// <param name="id">The identifier of the policy to be deleted.</param>
        [HttpDelete("{id}")]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> Delete(Guid id)
        {
            var policy = await _policyRepository.GetByIdAsync(id);
            if(policy == null || policy.OrganizationId != _currentContext.OrganizationId)
            {
                return new NotFoundResult();
            }
            await _policyRepository.DeleteAsync(policy);
            return new OkResult();
        }
    }
}
