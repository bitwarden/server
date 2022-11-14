using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.Groups.Interfaces;
using Bit.Core.Repositories;
using Bit.Scim.Groups.Interfaces;
using Bit.Scim.Models;
using Bit.Scim.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Scim.Controllers.v2;

[Authorize("Scim")]
[Route("v2/{organizationId}/groups")]
[ExceptionHandlerFilter]
public class GroupsController : Controller
{
    private readonly IGroupRepository _groupRepository;
    private readonly IGetGroupsListQuery _getGroupsListQuery;
    private readonly IDeleteGroupCommand _deleteGroupCommand;
    private readonly IPatchGroupCommand _patchGroupCommand;
    private readonly IPostGroupCommand _postGroupCommand;
    private readonly IPutGroupCommand _putGroupCommand;
    private readonly ILogger<GroupsController> _logger;

    public GroupsController(
        IGroupRepository groupRepository,
        IGetGroupsListQuery getGroupsListQuery,
        IDeleteGroupCommand deleteGroupCommand,
        IPatchGroupCommand patchGroupCommand,
        IPostGroupCommand postGroupCommand,
        IPutGroupCommand putGroupCommand,
        ILogger<GroupsController> logger)
    {
        _groupRepository = groupRepository;
        _getGroupsListQuery = getGroupsListQuery;
        _deleteGroupCommand = deleteGroupCommand;
        _patchGroupCommand = patchGroupCommand;
        _postGroupCommand = postGroupCommand;
        _putGroupCommand = putGroupCommand;
        _logger = logger;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid organizationId, Guid id)
    {
        var group = await _groupRepository.GetByIdAsync(id);
        if (group == null || group.OrganizationId != organizationId)
        {
            throw new NotFoundException("Group not found.");
        }
        return Ok(new ScimGroupResponseModel(group));
    }

    [HttpGet("")]
    public async Task<IActionResult> Get(
        Guid organizationId,
        [FromQuery] string filter,
        [FromQuery] int? count,
        [FromQuery] int? startIndex)
    {
        var groupsListQueryResult = await _getGroupsListQuery.GetGroupsListAsync(organizationId, filter, count, startIndex);
        var scimListResponseModel = new ScimListResponseModel<ScimGroupResponseModel>
        {
            Resources = groupsListQueryResult.groupList.Select(g => new ScimGroupResponseModel(g)).ToList(),
            ItemsPerPage = count.GetValueOrDefault(groupsListQueryResult.groupList.Count()),
            TotalResults = groupsListQueryResult.totalResults,
            StartIndex = startIndex.GetValueOrDefault(1),
        };
        return Ok(scimListResponseModel);
    }

    [HttpPost("")]
    public async Task<IActionResult> Post(Guid organizationId, [FromBody] ScimGroupRequestModel model)
    {
        var group = await _postGroupCommand.PostGroupAsync(organizationId, model);
        var scimGroupResponseModel = new ScimGroupResponseModel(group);
        return new CreatedResult(Url.Action(nameof(Get), new { group.OrganizationId, group.Id }), scimGroupResponseModel);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Put(Guid organizationId, Guid id, [FromBody] ScimGroupRequestModel model)
    {
        var group = await _putGroupCommand.PutGroupAsync(organizationId, id, model);
        var response = new ScimGroupResponseModel(group);

        return Ok(response);
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> Patch(Guid organizationId, Guid id, [FromBody] ScimPatchModel model)
    {
        await _patchGroupCommand.PatchGroupAsync(organizationId, id, model);
        return new NoContentResult();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid organizationId, Guid id)
    {
        await _deleteGroupCommand.DeleteGroupAsync(organizationId, id, EventSystemUser.SCIM);
        return new NoContentResult();
    }
}
