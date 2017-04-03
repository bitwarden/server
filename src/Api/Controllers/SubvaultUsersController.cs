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
    [Route("organizations/{orgId}/subvaultUsers")]
    [Authorize("Application")]
    public class SubvaultUsersController : Controller
    {
        private readonly ISubvaultRepository _subvaultRepository;
        private readonly ISubvaultUserRepository _subvaultUserRepository;
        private readonly IUserService _userService;

        public SubvaultUsersController(
            ISubvaultRepository subvaultRepository,
            ISubvaultUserRepository subvaultUserRepository,
            IUserService userService)
        {
            _subvaultRepository = subvaultRepository;
            _subvaultUserRepository = subvaultUserRepository;
            _userService = userService;
        }

        [HttpGet("{subvaultId}")]
        public async Task<ListResponseModel<SubvaultUserResponseModel>> GetBySubvault(string orgId, string subvaultId)
        {
            // TODO: permission check
            var subvaultUsers = await _subvaultUserRepository.GetManyDetailsBySubvaultIdAsync(new Guid(subvaultId));
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

            // TODO: permission check
            await _subvaultUserRepository.DeleteAsync(user);
        }
    }
}
