using System.Text.Json;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Scim.Commands.Groups;
using Bit.Scim.Commands.Users;
using Bit.Scim.Context;
using Bit.Scim.Models;
using Bit.Scim.Queries.Groups;
using Bit.Scim.Queries.Users;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Bit.Scim.Controllers.v2
{
    [Authorize("Scim")]
    [Route("v2/{organizationId}/groups")]
    public class GroupsController : Controller
    {
        private readonly ScimSettings _scimSettings;
        private readonly IGroupRepository _groupRepository;
        private readonly IGroupService _groupService;
        private readonly IScimContext _scimContext;
        private readonly ILogger<GroupsController> _logger;
        private readonly IMediator _mediator;

        public GroupsController(
            IGroupRepository groupRepository,
            IGroupService groupService,
            IOptions<ScimSettings> scimSettings,
            IScimContext scimContext,
            ILogger<GroupsController> logger,
            IMediator mediator)
        {
            _scimSettings = scimSettings?.Value;
            _groupRepository = groupRepository;
            _groupService = groupService;
            _scimContext = scimContext;
            _logger = logger;
            _mediator = mediator;
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid organizationId, Guid id)
        {
            var result = await _mediator.Send(new GetGroupQuery(organizationId, id));
            return StatusCode((int)result.StatusCode, result.Data);
        }

        [HttpGet("")]
        public async Task<IActionResult> Get(
            Guid organizationId,
            [FromQuery] string filter,
            [FromQuery] int? count,
            [FromQuery] int? startIndex)
        {
            var result = await _mediator.Send(new GetGroupsListQuery(organizationId, filter, count, startIndex));
            return StatusCode((int)result.StatusCode, result.Data);
        }

        [HttpPost("")]
        public async Task<IActionResult> Post(Guid organizationId, [FromBody] ScimGroupRequestModel model)
        {
            var result = await _mediator.Send(new PostGroupCommand(organizationId, model));
            if (!result.Success)
            {
                return StatusCode((int)result.StatusCode);
            }

            var group = result.Data;
            var response = new ScimGroupResponseModel(group);
            return new CreatedResult(Url.Action(nameof(Get), new { group.OrganizationId, group.Id }), response);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Put(Guid organizationId, Guid id, [FromBody] ScimGroupRequestModel model)
        {
            var result = await _mediator.Send(new PutGroupCommand(organizationId, id, model));
            return StatusCode((int)result.StatusCode, result.Data);
        }

        [HttpPatch("{id}")]
        public async Task<IActionResult> Patch(Guid organizationId, Guid id, [FromBody] ScimPatchModel model)
        {
            var result = await _mediator.Send(new PatchGroupCommand(organizationId, id, model));
            if (!result.Success)
            {
                return StatusCode((int)result.StatusCode, result.Data);
            }

            return new NoContentResult();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid organizationId, Guid id)
        {
            var result = await _mediator.Send(new DeleteGroupCommand(organizationId, id));
            if (!result.Success)
            {
                return StatusCode((int)result.StatusCode, result.Data);
            }

            return new NoContentResult();
        }
    }
}
