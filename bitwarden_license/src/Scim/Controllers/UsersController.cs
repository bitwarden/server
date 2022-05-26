using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bit.Scim.Controllers
{
    [Route("users")]
    public class UsersController : Controller
    {
        private readonly ScimSettings _scimSettings;
        private readonly ILogger<UsersController> _logger;

        public UsersController(
            IOptions<ScimSettings> billingSettings,
            ILogger<UsersController> logger)
        {
            _scimSettings = billingSettings?.Value;
            _logger = logger;
        }

        [HttpPost("")]
        public async Task<IActionResult> PostCreate([FromBody] object model)
        {
            return new CreatedResult("", new { });
        }
    }
}
