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
    [Route("organizations/{orgId}/subvaults")]
    [Authorize("Application")]
    public class SubvaultsController : Controller
    {
        private readonly ISubvaultRepository _subvaultRepository;
        private readonly ISubvaultService _subvaultService;
        private readonly IUserService _userService;
        private readonly CurrentContext _currentContext;

        public SubvaultsController(
            ISubvaultRepository subvaultRepository,
            ISubvaultService subvaultService,
            IUserService userService,
            CurrentContext currentContext)
        {
            _subvaultRepository = subvaultRepository;
            _subvaultService = subvaultService;
            _userService = userService;
            _currentContext = currentContext;
        }

        [HttpGet("{id}")]
        public async Task<SubvaultResponseModel> Get(string orgId, string id)
        {
            var subvault = await _subvaultRepository.GetByIdAsync(new Guid(id));
            if(subvault == null || !_currentContext.OrganizationAdmin(subvault.OrganizationId))
            {
                throw new NotFoundException();
            }

            return new SubvaultResponseModel(subvault);
        }

        [HttpGet("")]
        public async Task<ListResponseModel<SubvaultResponseModel>> Get(string orgId)
        {
            var orgIdGuid = new Guid(orgId);
            if(!_currentContext.OrganizationAdmin(orgIdGuid))
            {
                throw new NotFoundException();
            }

            var subvaults = await _subvaultRepository.GetManyByOrganizationIdAsync(orgIdGuid);
            var responses = subvaults.Select(s => new SubvaultResponseModel(s));
            return new ListResponseModel<SubvaultResponseModel>(responses);
        }

        [HttpGet("~/subvaults")]
        public async Task<ListResponseModel<SubvaultResponseModel>> GetUser()
        {
            var subvaults = await _subvaultRepository.GetManyByUserIdAsync(_userService.GetProperUserId(User).Value);
            var responses = subvaults.Select(s => new SubvaultResponseModel(s));
            return new ListResponseModel<SubvaultResponseModel>(responses);
        }

        [HttpPost("")]
        public async Task<SubvaultResponseModel> Post(string orgId, [FromBody]SubvaultRequestModel model)
        {
            var orgIdGuid = new Guid(orgId);
            if(!_currentContext.OrganizationAdmin(orgIdGuid))
            {
                throw new NotFoundException();
            }

            var subvault = model.ToSubvault(orgIdGuid);
            await _subvaultService.SaveAsync(subvault);
            return new SubvaultResponseModel(subvault);
        }

        [HttpPut("{id}")]
        [HttpPost("{id}")]
        public async Task<SubvaultResponseModel> Put(string orgId, string id, [FromBody]SubvaultRequestModel model)
        {
            var subvault = await _subvaultRepository.GetByIdAsync(new Guid(id));
            if(subvault == null || !_currentContext.OrganizationAdmin(subvault.OrganizationId))
            {
                throw new NotFoundException();
            }

            await _subvaultService.SaveAsync(model.ToSubvault(subvault));
            return new SubvaultResponseModel(subvault);
        }

        [HttpDelete("{id}")]
        [HttpPost("{id}/delete")]
        public async Task Delete(string orgId, string id)
        {
            var subvault = await _subvaultRepository.GetByIdAsync(new Guid(id));
            if(subvault == null || !_currentContext.OrganizationAdmin(subvault.OrganizationId))
            {
                throw new NotFoundException();
            }

            await _subvaultRepository.DeleteAsync(subvault);
        }
    }
}
