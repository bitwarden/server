using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bit.Scim.Controllers.v2
{
    [Route("v2/{organizationId}/groups")]
    public class GroupsController : Controller
    {
        private readonly ScimSettings _scimSettings;
        private readonly ILogger<GroupsController> _logger;

        public GroupsController(
            IOptions<ScimSettings> scimSettings,
            ILogger<GroupsController> logger)
        {
            _scimSettings = scimSettings?.Value;
            _logger = logger;
        }

        [HttpPost("")]
        public async Task<IActionResult> PostCreate(Guid organizationId, [FromBody] object model)
        {
            return new CreatedResult("", new { });
        }
    }
}
