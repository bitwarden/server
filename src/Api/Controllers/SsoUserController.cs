using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Bit.Core.Repositories;
using Bit.Core.Models.Table;
using Bit.Core.Exceptions;
using Bit.Core.Services;

namespace Bit.Api.Controllers
{
    [Route("sso-user")]
    public class SsoUserController : Controller
    {
        private readonly IUserService _userService;
        private readonly ISsoUserRepository _ssoUserRepository;

        public SsoUserController(IUserService userService, ISsoUserRepository ssoUserRepository)
        {
            _userService = userService;
            _ssoUserRepository = ssoUserRepository;
        }

        [HttpDelete("{organizationId}")]
        public async Task Delete(string organizationId)
        {
            var userId = _userService.GetProperUserId(User);
            if (!userId.HasValue) {
                throw new NotFoundException();
            }

            var ssoUser = new SsoUser() {
                UserId = userId.Value,
                OrganizationId = new Guid(organizationId),
            };

            await _ssoUserRepository.DeleteAsync(ssoUser);
        }
    }
}
