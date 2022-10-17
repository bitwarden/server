using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Scim.Context;
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
    private readonly IGroupService _groupService;
    private readonly IScimContext _scimContext;
    private readonly IGetGroupsListQuery _getGroupsListQuery;
    private readonly IPatchGroupCommand _patchGroupCommand;
    private readonly IPutGroupCommand _putGroupCommand;
    private readonly ILogger<GroupsController> _logger;

    public GroupsController(
        IGroupRepository groupRepository,
        IGroupService groupService,
        IScimContext scimContext,
        IGetGroupsListQuery getGroupsListQuery,
        IPatchGroupCommand patchGroupCommand,
        IPutGroupCommand putGroupCommand,
        ILogger<GroupsController> logger)
    {
        _groupRepository = groupRepository;
        _groupService = groupService;
        _scimContext = scimContext;
        _getGroupsListQuery = getGroupsListQuery;
        _patchGroupCommand = patchGroupCommand;
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
        if (string.IsNullOrWhiteSpace(model.DisplayName))
        {
            return new BadRequestResult();
        }

        var groups = await _groupRepository.GetManyByOrganizationIdAsync(organizationId);
        if (!string.IsNullOrWhiteSpace(model.ExternalId) && groups.Any(g => g.ExternalId == model.ExternalId))
        {
            return new ConflictResult();
        }

        var group = model.ToGroup(organizationId);
        await _groupService.SaveAsync(group, null);
        await UpdateGroupMembersAsync(group, model, true);
        var response = new ScimGroupResponseModel(group);
        return new CreatedResult(Url.Action(nameof(Get), new { group.OrganizationId, group.Id }), response);
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
        var group = await _groupRepository.GetByIdAsync(id);
        if (group == null || group.OrganizationId != organizationId)
        {
            return new NotFoundObjectResult(new ScimErrorResponseModel
            {
                Status = 404,
                Detail = "Group not found."
            });
        }
        await _groupService.DeleteAsync(group);
        return new NoContentResult();
    }

    private async Task UpdateGroupMembersAsync(Group group, ScimGroupRequestModel model, bool skipIfEmpty)
    {
        if (_scimContext.RequestScimProvider != Core.Enums.ScimProviderType.Okta)
        {
            return;
        }

        if (model.Members == null)
        {
            return;
        }

        var memberIds = new List<Guid>();
        foreach (var id in model.Members.Select(i => i.Value))
        {
            if (Guid.TryParse(id, out var guidId))
            {
                memberIds.Add(guidId);
            }
        }

        if (!memberIds.Any() && skipIfEmpty)
        {
            return;
        }

        await _groupRepository.UpdateUsersAsync(group.Id, memberIds);
    }
}
