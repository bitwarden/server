using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Scim.Commands.Users;
using Bit.Scim.Context;
using Bit.Scim.Models;
using Bit.Scim.Queries.Users;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Bit.Scim.Controllers.v2
{
    [Authorize("Scim")]
    [Route("v2/{organizationId}/users")]
    public class UsersController : Controller
    {
        private readonly IUserService _userService;
        private readonly IUserRepository _userRepository;
        private readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly IOrganizationService _organizationService;
        private readonly IScimContext _scimContext;
        private readonly ScimSettings _scimSettings;
        private readonly ILogger<UsersController> _logger;
        private readonly IMediator _mediator;

        public UsersController(
            IUserService userService,
            IUserRepository userRepository,
            IOrganizationUserRepository organizationUserRepository,
            IOrganizationService organizationService,
            IScimContext scimContext,
            IOptions<ScimSettings> scimSettings,
            ILogger<UsersController> logger,
            IMediator mediator)
        {
            _userService = userService;
            _userRepository = userRepository;
            _organizationUserRepository = organizationUserRepository;
            _organizationService = organizationService;
            _scimContext = scimContext;
            _scimSettings = scimSettings?.Value;
            _logger = logger;
            _mediator = mediator;
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid organizationId, Guid id)
        {
            var result = await _mediator.Send(new GetUserQuery(organizationId, id));
            return StatusCode((int)result.StatusCode, result.Data);
        }

        [HttpGet("")]
        public async Task<IActionResult> Get(
            Guid organizationId,
            [FromQuery] string filter,
            [FromQuery] int? count,
            [FromQuery] int? startIndex)
        {
            var result = await _mediator.Send(new GetUsersListQuery(organizationId, filter, count, startIndex));
            return StatusCode((int)result.StatusCode, result.Data);
        }

        [HttpPost("")]
        public async Task<IActionResult> Post(Guid organizationId, [FromBody] ScimUserRequestModel model)
        {
            var result = await _mediator.Send(new PostUserCommand(organizationId, model));
            if (!result.Success)
            {
                return StatusCode((int)result.StatusCode);
            }

            var orgUser = result.Data;
            var response = new ScimUserResponseModel(orgUser);
            return new CreatedResult(Url.Action(nameof(Get), new { orgUser.OrganizationId, orgUser.Id }), response);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Put(Guid organizationId, Guid id, [FromBody] ScimUserRequestModel model)
        {
            var result = await _mediator.Send(new PutUserCommand(organizationId, id, model));
            return StatusCode((int)result.StatusCode, result.Data);
        }

        [HttpPatch("{id}")]
        public async Task<IActionResult> Patch(Guid organizationId, Guid id, [FromBody] ScimPatchModel model)
        {
            var result = await _mediator.Send(new PatchUserCommand(organizationId, id, model));
            if (!result.Success)
            {
                return StatusCode((int)result.StatusCode, result.Data);
            }

            return new NoContentResult();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid organizationId, Guid id, [FromBody] ScimUserRequestModel model)
        {
            var result = await _mediator.Send(new DeleteUserCommand(organizationId, id, model));
            if (!result.Success)
            {
                return StatusCode((int)result.StatusCode, result.Data);
            }

            return new NoContentResult();
        }
    }
}
