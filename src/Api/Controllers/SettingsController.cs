using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Bit.Core.Models.Api;
using Bit.Core.Services;
using Bit.Core.Models.Api.Response;

namespace Bit.Api.Controllers
{
    [Route("settings")]
    [Authorize("Application")]
    public class SettingsController : Controller
    {
        private readonly IUserService _userService;

        public SettingsController(
            IUserService userService)
        {
            _userService = userService;
        }

        [HttpGet("domains")]
        public async Task<DomainsResponseModel> GetDomains(bool excluded = true)
        {
            var user = await _userService.GetUserByPrincipalAsync(User);
            if (user == null)
            {
                throw new UnauthorizedAccessException();
            }

            var response = new DomainsResponseModel(user, excluded);
            return response;
        }

        [HttpPut("domains")]
        [HttpPost("domains")]
        public async Task<DomainsResponseModel> PutDomains([FromBody]UpdateDomainsRequestModel model)
        {
            var user = await _userService.GetUserByPrincipalAsync(User);
            if (user == null)
            {
                throw new UnauthorizedAccessException();
            }

            await _userService.SaveUserAsync(model.ToUser(user), true);

            var response = new DomainsResponseModel(user);
            return response;
        }

        [HttpGet("usernames")]
        public async Task<UsernamesResponseModel> GetUsernames()
        {
            var user = await _userService.GetUserByPrincipalAsync(User);
            if (user == null)
            {
                throw new UnauthorizedAccessException();
            }

            var response = new UsernamesResponseModel(user);
            return response;
        }

        [HttpPut("usernames")]
        [HttpPost("usernames")]
        public async Task<UsernamesResponseModel> PutUsernames([FromBody] UpdateUsernamessRequestModel model)
        {
            var user = await _userService.GetUserByPrincipalAsync(User);
            if (user == null)
            {
                throw new UnauthorizedAccessException();
            }

            await _userService.SaveUserAsync(model.ToUser(user), true);

            var response = new UsernamesResponseModel(user);
            return response;
        }
    }
}
