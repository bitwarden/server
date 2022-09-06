using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Scim.Commands.Groups.Interfaces;
using Bit.Scim.Context;
using Bit.Scim.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Bit.Scim.Controllers.v2;

[Authorize("Scim")]
[Route("v2/{organizationId}/groups")]
public class GroupsController : Controller
{
    private readonly ScimSettings _scimSettings;
    private readonly IGroupRepository _groupRepository;
    private readonly IGroupService _groupService;
    private readonly IScimContext _scimContext;
    private readonly ILogger<GroupsController> _logger;
    private readonly IGetGroupCommand _getGroupCommand;
    private readonly IGetGroupsListCommand _getGroupsListCommand;
    private readonly IPostGroupCommand _postGroupCommand;
    private readonly IPutGroupCommand _putGroupCommand;
    private readonly IPatchGroupCommand _patchGroupCommand;
    private readonly IDeleteGroupCommand _deleteGroupCommand;

    public GroupsController(
        IGroupRepository groupRepository,
        IGroupService groupService,
        IOptions<ScimSettings> scimSettings,
        IScimContext scimContext,
        IGetGroupCommand getGroupCommand,
        IGetGroupsListCommand getGroupsListCommand,
        IPostGroupCommand postGroupCommand,
        IPutGroupCommand putGroupCommand,
        IPatchGroupCommand patchGroupCommand,
        IDeleteGroupCommand deleteGroupCommand,
        ILogger<GroupsController> logger)
    {
        _scimSettings = scimSettings?.Value;
        _groupRepository = groupRepository;
        _groupService = groupService;
        _scimContext = scimContext;
        _getGroupCommand = getGroupCommand;
        _getGroupsListCommand = getGroupsListCommand;
        _postGroupCommand = postGroupCommand;
        _putGroupCommand = putGroupCommand;
        _patchGroupCommand = patchGroupCommand;
        _deleteGroupCommand = deleteGroupCommand;
        _logger = logger;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid organizationId, Guid id)
    {
        try
        {
            var scimGroupResponseModel = await _getGroupCommand.GetGroupAsync(organizationId, id);
            return Ok(scimGroupResponseModel);
        }
        catch (NotFoundException ex)
        {
            return NotFound(new ScimErrorResponseModel
            {
                Status = 404,
                Detail = ex.Message
            });
        }
    }

    [HttpGet("")]
    public async Task<IActionResult> Get(
        Guid organizationId,
        [FromQuery] string filter,
        [FromQuery] int? count,
        [FromQuery] int? startIndex)
    {
        var scimListResponseModel = await _getGroupsListCommand.GetGroupsListAsync(organizationId, filter, count, startIndex);
        return Ok(scimListResponseModel);
    }

    [HttpPost("")]
    public async Task<IActionResult> Post(Guid organizationId, [FromBody] ScimGroupRequestModel model)
    {
        try
        {
            var group = await _postGroupCommand.PostGroupAsync(organizationId, model);
            var scimGroupResponseModel = new ScimGroupResponseModel(group);
            return new CreatedResult(Url.Action(nameof(Get), new { group.OrganizationId, group.Id }), scimGroupResponseModel);
        }
        catch (BadRequestException)
        {
            return BadRequest();
        }
        catch (ConflictException)
        {
            return Conflict();
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Put(Guid organizationId, Guid id, [FromBody] ScimGroupRequestModel model)
    {
        try
        {
            var scimGroupResponseModel = await _putGroupCommand.PutGroupAsync(organizationId, id, model);
            return Ok(scimGroupResponseModel);
        }
        catch (NotFoundException ex)
        {
            return NotFound(new ScimErrorResponseModel
            {
                Status = 404,
                Detail = ex.Message
            });
        }
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> Patch(Guid organizationId, Guid id, [FromBody] ScimPatchModel model)
    {
        try
        {
            await _patchGroupCommand.PatchGroupAsync(organizationId, id, model);
            return new NoContentResult();
        }
        catch (NotFoundException ex)
        {
            return NotFound(new ScimErrorResponseModel
            {
                Status = 404,
                Detail = ex.Message
            });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid organizationId, Guid id)
    {
        try
        {
            await _deleteGroupCommand.DeleteGroupAsync(organizationId, id);
            return new NoContentResult();
        }
        catch (NotFoundException ex)
        {
            return NotFound(new ScimErrorResponseModel
            {
                Status = 404,
                Detail = ex.Message
            });
        }
    }
}
