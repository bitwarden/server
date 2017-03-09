using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Authorization;
using Bit.Core.Models.Api;
using Bit.Core.Exceptions;
using Bit.Core.Services;

namespace Bit.Api.Controllers
{
    [Route("subvaults")]
    [Authorize("Application")]
    public class SubvaultsController : Controller
    {
        private readonly ISubvaultRepository _subvaultRepository;
        private readonly IUserService _userService;

        public SubvaultsController(
            ISubvaultRepository subvaultRepository,
            IUserService userService)
        {
            _subvaultRepository = subvaultRepository;
            _userService = userService;
        }

        [HttpGet("{id}")]
        public async Task<SubvaultResponseModel> Get(string id)
        {
            var userId = _userService.GetProperUserId(User).Value;
            var subvault = await _subvaultRepository.GetByIdAdminUserIdAsync(new Guid(id), userId);
            if(subvault == null)
            {
                throw new NotFoundException();
            }

            return new SubvaultResponseModel(subvault);
        }

        [HttpGet("")]
        public async Task<ListResponseModel<SubvaultResponseModel>> Get()
        {
            var subvaults = await _subvaultRepository.GetManyByUserIdAsync(_userService.GetProperUserId(User).Value);
            var responses = subvaults.Select(s => new SubvaultResponseModel(s));
            return new ListResponseModel<SubvaultResponseModel>(responses);
        }

        [HttpGet("organization/{organizationId}")]
        public async Task<ListResponseModel<SubvaultResponseModel>> GetByOrganization(string organizationId)
        {
            var subvaults = await _subvaultRepository.GetManyByOrganizationIdAdminUserIdAsync(new Guid(organizationId),
                _userService.GetProperUserId(User).Value);
            var responses = subvaults.Select(s => new SubvaultResponseModel(s));
            return new ListResponseModel<SubvaultResponseModel>(responses);
        }

        [HttpPost("")]
        public async Task<SubvaultResponseModel> Post([FromBody]SubvaultCreateRequestModel model)
        {
            // TODO: permission check
            var subvault = model.ToSubvault();
            await _subvaultRepository.CreateAsync(subvault);
            return new SubvaultResponseModel(subvault);
        }

        [HttpPut("{id}")]
        [HttpPost("{id}")]
        public async Task<SubvaultResponseModel> Put(string id, [FromBody]SubvaultUpdateRequestModel model)
        {
            var subvault = await _subvaultRepository.GetByIdAdminUserIdAsync(new Guid(id),
                _userService.GetProperUserId(User).Value);
            if(subvault == null)
            {
                throw new NotFoundException();
            }

            await _subvaultRepository.ReplaceAsync(model.ToSubvault(subvault));
            return new SubvaultResponseModel(subvault);
        }

        [HttpDelete("{id}")]
        [HttpPost("{id}/delete")]
        public async Task Delete(string id)
        {
            var subvault = await _subvaultRepository.GetByIdAdminUserIdAsync(new Guid(id),
                _userService.GetProperUserId(User).Value);
            if(subvault == null)
            {
                throw new NotFoundException();
            }

            await _subvaultRepository.DeleteAsync(subvault);
        }
    }
}
