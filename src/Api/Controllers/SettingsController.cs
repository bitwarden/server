using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Bit.Api.Models;
using Bit.Core.Services;
using Bit.Core;

namespace Bit.Api.Controllers
{
    [Route("settings")]
    [Authorize("Application")]
    public class SettingsController : Controller
    {
        private readonly IUserService _userService;
        private readonly CurrentContext _currentContext;

        public SettingsController(
            IUserService userService,
            CurrentContext currentContext)
        {
            _userService = userService;
            _currentContext = currentContext;
        }

        [HttpGet("domains")]
        public Task<DomainsResponseModel> GetDomains()
        {
            var response = new DomainsResponseModel(_currentContext.User);
            return Task.FromResult(response);
        }

        [HttpPut("domains")]
        [HttpPost("domains")]
        public async Task<DomainsResponseModel> PutDomains([FromBody]UpdateDomainsRequestModel model)
        {
            await _userService.SaveUserAsync(model.ToUser(_currentContext.User));

            var response = new DomainsResponseModel(_currentContext.User);
            return response;
        }
    }
}
