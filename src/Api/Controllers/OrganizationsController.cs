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
        private readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly IOrganizationService _organizationService;
        private readonly IUserService _userService;

        public OrganizationsController(
            IOrganizationRepository organizationRepository,
            IOrganizationUserRepository organizationUserRepository,
            IOrganizationService organizationService,
            IUserService userService)
        {
            _organizationRepository = organizationRepository;
            _organizationUserRepository = organizationUserRepository;
            _organizationService = organizationService;
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

        [HttpGet("{id}/extended")]
        public async Task<OrganizationExtendedResponseModel> GetExtended(string id)
        {
            var userId = _userService.GetProperUserId(User).Value;
            var organization = await _organizationRepository.GetByIdAsync(new Guid(id), userId);
            if(organization == null)
            {
                throw new NotFoundException();
            }

            var organizationUser = await _organizationUserRepository.GetByOrganizationAsync(new Guid(id), userId);
            if(organizationUser == null)
            {
                throw new NotFoundException();
            }

            return new OrganizationExtendedResponseModel(organization, organizationUser);
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
        public async Task<OrganizationExtendedResponseModel> Post([FromBody]OrganizationCreateRequestModel model)
        {
            var user = await _userService.GetUserByPrincipalAsync(User);
            var organizationSignup = model.ToOrganizationSignup(user);
            var result = await _organizationService.SignUpAsync(organizationSignup);
            return new OrganizationExtendedResponseModel(result.Item1, result.Item2);
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
