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
    [Route("organizations/{orgId}/subvaultUsers")]
    [Authorize("Application")]
    public class SubvaultUsersController : Controller
    {
        private readonly ISubvaultRepository _subvaultRepository;
        private readonly ISubvaultUserRepository _subvaultUserRepository;
        private readonly IUserService _userService;
        private readonly CurrentContext _currentContext;

        public SubvaultUsersController(
            ISubvaultRepository subvaultRepository,
            ISubvaultUserRepository subvaultUserRepository,
            IUserService userService,
            CurrentContext currentContext)
        {
            _subvaultRepository = subvaultRepository;
            _subvaultUserRepository = subvaultUserRepository;
            _userService = userService;
            _currentContext = currentContext;
        }

        [HttpGet("{subvaultId}")]
        public async Task<ListResponseModel<SubvaultUserResponseModel>> GetBySubvault(string orgId, string subvaultId)
        {
            var subvaultIdGuid = new Guid(subvaultId);
            var subvault = await _subvaultRepository.GetByIdAsync(subvaultIdGuid);
            if(subvault == null || !_currentContext.OrganizationAdmin(subvault.OrganizationId))
            {
                throw new NotFoundException();
            }

            var subvaultUsers = await _subvaultUserRepository.GetManyDetailsBySubvaultIdAsync(subvaultIdGuid);
            var responses = subvaultUsers.Select(s => new SubvaultUserResponseModel(s));
            return new ListResponseModel<SubvaultUserResponseModel>(responses);
        }

        [HttpDelete("{id}")]
        [HttpPost("{id}/delete")]
        public async Task Delete(string orgId, string id)
        {
            var user = await _subvaultUserRepository.GetByIdAsync(new Guid(id));
            if(user == null)
            {
                throw new NotFoundException();
            }

            var subvault = await _subvaultRepository.GetByIdAsync(user.SubvaultId);
            if(subvault == null || !_currentContext.OrganizationAdmin(subvault.OrganizationId))
            {
                throw new NotFoundException();
            }

            await _subvaultUserRepository.DeleteAsync(user);
        }
    }
}
