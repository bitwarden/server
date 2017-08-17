using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Bit.Api.Utilities;
using Microsoft.AspNetCore.Authorization;
using Bit.Core.Services;

namespace Bit.Api.Controllers
{
    [Route("jobs")]
    [SelfHosted(SelfHostedOnly = true)]
    [AllowAnonymous]
    public class JobsController : Controller
    {
        private readonly ILicensingService _licensingService;
        private readonly IUserService _userService;

        public JobsController(
            ILicensingService licensingService,
            IUserService userService)
        {
            _licensingService = licensingService;
            _userService = userService;
        }

        [HttpPost("organization-license")]
        public async Task PostOrganizationLicense()
        {
            await _licensingService.ValidateOrganizationsAsync();
        }

        [HttpPost("refresh-licenses")]
        public Task PostRefreshLicenses()
        {
            // TODO
            return Task.FromResult(0);
        }
    }
}
