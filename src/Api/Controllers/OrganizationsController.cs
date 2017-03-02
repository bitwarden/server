using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Authorization;
using Bit.Api.Models;
using Bit.Core.Exceptions;
using Bit.Core.Services;

namespace Bit.Api.Controllers
{
    [Route("organizations")]
    [Authorize("Application")]
    public class OrganizationsController : Controller
    {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IUserService _userService;

        public OrganizationsController(
            IOrganizationRepository organizationRepository,
            IUserService userService)
        {
            _organizationRepository = organizationRepository;
            _userService = userService;
        }

        [HttpGet("{id}")]
        public async Task<OrganizationResponseModel> Get(string id)
        {
            var userId = _userService.GetProperUserId(User).Value;
            var organization = await _organizationRepository.GetByIdAsync(new Guid(id), userId);
            if(organization == null)
            {
                throw new NotFoundException();
            }

            return new OrganizationResponseModel(organization);
        }

        [HttpGet("")]
        public async Task<ListResponseModel<OrganizationResponseModel>> Get()
        {
            var userId = _userService.GetProperUserId(User).Value;
            var organizations = await _organizationRepository.GetManyByUserIdAsync(userId);
            var responses = organizations.Select(o => new OrganizationResponseModel(o));
            return new ListResponseModel<OrganizationResponseModel>(responses);
        }

        [HttpPost("")]
        public async Task<OrganizationResponseModel> Post([FromBody]OrganizationCreateRequestModel model)
        {
            var userId = _userService.GetProperUserId(User).Value;
            var organization = model.ToOrganization(_userService.GetProperUserId(User).Value);
            await _organizationRepository.ReplaceAsync(organization);
            return new OrganizationResponseModel(organization);
        }

        [HttpPut("{id}")]
        [HttpPost("{id}")]
        public async Task<OrganizationResponseModel> Put(string id, [FromBody]OrganizationUpdateRequestModel model)
        {
            var userId = _userService.GetProperUserId(User).Value;
            var organization = await _organizationRepository.GetByIdAsync(new Guid(id), userId);
            // TODO: Permission checks
            if(organization == null)
            {
                throw new NotFoundException();
            }

            await _organizationRepository.ReplaceAsync(model.ToOrganization(organization));
            return new OrganizationResponseModel(organization);
        }

        [HttpDelete("{id}")]
        [HttpPost("{id}/delete")]
        public async Task Delete(string id)
        {
            var organization = await _organizationRepository.GetByIdAsync(new Guid(id),
                _userService.GetProperUserId(User).Value);
            if(organization == null)
            {
                throw new NotFoundException();
            }

            await _organizationRepository.DeleteAsync(organization);
        }
    }
}
