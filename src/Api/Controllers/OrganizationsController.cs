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
    [Route("organizations")]
    [Authorize("Application")]
    public class OrganizationsController : Controller
    {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly IOrganizationService _organizationService;
        private readonly IUserService _userService;
        private readonly CurrentContext _currentContext;

        public OrganizationsController(
            IOrganizationRepository organizationRepository,
            IOrganizationUserRepository organizationUserRepository,
            IOrganizationService organizationService,
            IUserService userService,
            CurrentContext currentContext)
        {
            _organizationRepository = organizationRepository;
            _organizationUserRepository = organizationUserRepository;
            _organizationService = organizationService;
            _userService = userService;
            _currentContext = currentContext;
        }

        [HttpGet("{id}")]
        public async Task<OrganizationResponseModel> Get(string id)
        {
            var orgIdGuid = new Guid(id);
            if(!_currentContext.OrganizationOwner(orgIdGuid))
            {
                throw new NotFoundException();
            }

            var organization = await _organizationRepository.GetByIdAsync(orgIdGuid);
            if(organization == null)
            {
                throw new NotFoundException();
            }

            return new OrganizationResponseModel(organization);
        }

        [HttpGet("{id}/billing")]
        public async Task<OrganizationResponseModel> GetBilling(string id)
        {
            var orgIdGuid = new Guid(id);
            if(!_currentContext.OrganizationOwner(orgIdGuid))
            {
                throw new NotFoundException();
            }

            var organization = await _organizationRepository.GetByIdAsync(orgIdGuid);
            if(organization == null)
            {
                throw new NotFoundException();
            }

            // TODO: billing stuff

            return new OrganizationResponseModel(organization);
        }

        [HttpGet("")]
        public async Task<ListResponseModel<OrganizationResponseModel>> GetUser()
        {
            var userId = _userService.GetProperUserId(User).Value;
            var organizations = await _organizationRepository.GetManyByUserIdAsync(userId);
            var responses = organizations.Select(o => new OrganizationResponseModel(o));
            return new ListResponseModel<OrganizationResponseModel>(responses);
        }

        [HttpPost("")]
        public async Task<OrganizationResponseModel> Post([FromBody]OrganizationCreateRequestModel model)
        {
            var user = await _userService.GetUserByPrincipalAsync(User);
            var organizationSignup = model.ToOrganizationSignup(user);
            var result = await _organizationService.SignUpAsync(organizationSignup);
            return new OrganizationResponseModel(result.Item1);
        }

        [HttpPut("{id}")]
        [HttpPost("{id}")]
        public async Task<OrganizationResponseModel> Put(string id, [FromBody]OrganizationUpdateRequestModel model)
        {
            var orgIdGuid = new Guid(id);
            if(!_currentContext.OrganizationOwner(orgIdGuid))
            {
                throw new NotFoundException();
            }

            var organization = await _organizationRepository.GetByIdAsync(orgIdGuid);
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
            var orgIdGuid = new Guid(id);
            if(!_currentContext.OrganizationOwner(orgIdGuid))
            {
                throw new NotFoundException();
            }

            var organization = await _organizationRepository.GetByIdAsync(orgIdGuid);
            if(organization == null)
            {
                throw new NotFoundException();
            }

            await _organizationRepository.DeleteAsync(organization);
        }
    }
}
