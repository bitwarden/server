using Bit.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;

namespace Bit.Billing.Controllers
{
    [Route("jobs")]
    public class JobsController : Controller
    {
        private readonly BillingSettings _billingSettings;
        private readonly IOrganizationService _organizationService;
        private readonly IUserService _userService;

        public JobsController(
            IOptions<BillingSettings> billingSettings,
            IOrganizationService organizationService,
            IUserService userService)
        {
            _billingSettings = billingSettings?.Value;
            _organizationService = organizationService;
            _userService = userService;
        }

        [HttpPost("expirations")]
        public async Task<IActionResult> PostExpirations([FromQuery] string key)
        {
            if(key != _billingSettings.JobsKey)
            {
                return new BadRequestResult();
            }

            // TODO check for all users/orgs that are expired and disable them

            return new OkResult();
        }
    }
}
