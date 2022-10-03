using System.Text.Json;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Scim.Commands.Groups.Interfaces;
using Bit.Scim.Context;
using Bit.Scim.Models;
using Bit.Scim.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Bit.Scim.Controllers.v2;

[Authorize("Scim")]
[Route("v2/{organizationId}/groups")]
[ExceptionHandlerFilter]
public class GroupsController : Controller
{
    private readonly ScimSettings _scimSettings;
    private readonly IGroupRepository _groupRepository;
    private readonly IGroupService _groupService;
    private readonly IScimContext _scimContext;
    private readonly ILogger<GroupsController> _logger;
    private readonly IPatchGroupCommand _patchGroupCommand;

    public GroupsController(
        IGroupRepository groupRepository,
        IGroupService groupService,
        IOptions<ScimSettings> scimSettings,
        IScimContext scimContext,
        IPatchGroupCommand patchGroupCommand,
        ILogger<GroupsController> logger)
    {
        _scimSettings = scimSettings?.Value;
        _groupRepository = groupRepository;
        _groupService = groupService;
        _scimContext = scimContext;
        _patchGroupCommand = patchGroupCommand;
        _logger = logger;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid organizationId, Guid id)
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
        return new ObjectResult(new ScimGroupResponseModel(group));
    }

    [HttpGet("")]
    public async Task<IActionResult> Get(
        Guid organizationId,
        [FromQuery] string filter,
        [FromQuery] int? count,
        [FromQuery] int? startIndex)
    {
        string nameFilter = null;
        string externalIdFilter = null;
        if (!string.IsNullOrWhiteSpace(filter))
        {
            if (filter.StartsWith("displayName eq "))
            {
                nameFilter = filter.Substring(15).Trim('"');
            }
            else if (filter.StartsWith("externalId eq "))
            {
                externalIdFilter = filter.Substring(14).Trim('"');
            }
        }

        var groupList = new List<ScimGroupResponseModel>();
        var groups = await _groupRepository.GetManyByOrganizationIdAsync(organizationId);
        var totalResults = 0;
        if (!string.IsNullOrWhiteSpace(nameFilter))
        {
            var group = groups.FirstOrDefault(g => g.Name == nameFilter);
            if (group != null)
            {
                groupList.Add(new ScimGroupResponseModel(group));
            }
            totalResults = groupList.Count;
        }
        else if (!string.IsNullOrWhiteSpace(externalIdFilter))
        {
            var group = groups.FirstOrDefault(ou => ou.ExternalId == externalIdFilter);
            if (group != null)
            {
                groupList.Add(new ScimGroupResponseModel(group));
            }
            totalResults = groupList.Count;
        }
        else if (string.IsNullOrWhiteSpace(filter) && startIndex.HasValue && count.HasValue)
        {
            groupList = groups.OrderBy(g => g.Name)
                .Skip(startIndex.Value - 1)
                .Take(count.Value)
                .Select(g => new ScimGroupResponseModel(g))
                .ToList();
            totalResults = groups.Count;
        }

        var result = new ScimListResponseModel<ScimGroupResponseModel>
        {
            Resources = groupList,
            ItemsPerPage = count.GetValueOrDefault(groupList.Count),
            TotalResults = totalResults,
            StartIndex = startIndex.GetValueOrDefault(1),
        };
        return new ObjectResult(result);
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
        var group = await _groupRepository.GetByIdAsync(id);
        if (group == null || group.OrganizationId != organizationId)
        {
            return new NotFoundObjectResult(new ScimErrorResponseModel
            {
                Status = 404,
                Detail = "Group not found."
            });
        }

        group.Name = model.DisplayName;
        await _groupService.SaveAsync(group);
        await UpdateGroupMembersAsync(group, model, false);
        return new ObjectResult(new ScimGroupResponseModel(group));
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

    private List<Guid> GetOperationValueIds(JsonElement objArray)
    {
        var ids = new List<Guid>();
        foreach (var obj in objArray.EnumerateArray())
        {
            if (obj.TryGetProperty("value", out var valueProperty))
            {
                if (valueProperty.TryGetGuid(out var guid))
                {
                    ids.Add(guid);
                }
            }
        }
        return ids;
    }

    private Guid? GetOperationPathId(string path)
    {
        // Parse Guid from string like: members[value eq "{GUID}"}]
        if (Guid.TryParse(path.Substring(18).Replace("\"]", string.Empty), out var id))
        {
            return id;
        }
        return null;
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
