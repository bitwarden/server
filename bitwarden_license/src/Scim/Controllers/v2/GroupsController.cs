using System.Text.Json;
using Bit.Core.Entities;
using Bit.Core.OrganizationFeatures.Groups;
using Bit.Core.Repositories;
using Bit.Core.Services;
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
    private readonly IDeleteGroupCommand _deleteGroupCommand;
    private readonly ILogger<GroupsController> _logger;

    public GroupsController(
        IGroupRepository groupRepository,
        IGroupService groupService,
        IOptions<ScimSettings> scimSettings,
        IScimContext scimContext,
        IDeleteGroupCommand deleteGroupCommand,
        ILogger<GroupsController> logger)
    {
        _scimSettings = scimSettings?.Value;
        _groupRepository = groupRepository;
        _groupService = groupService;
        _scimContext = scimContext;
        _deleteGroupCommand = deleteGroupCommand;
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
        var group = await _groupRepository.GetByIdAsync(id);
        if (group == null || group.OrganizationId != organizationId)
        {
            return new NotFoundObjectResult(new ScimErrorResponseModel
            {
                Status = 404,
                Detail = "Group not found."
            });
        }

        var operationHandled = false;
        foreach (var operation in model.Operations)
        {
            // Replace operations
            if (operation.Op?.ToLowerInvariant() == "replace")
            {
                // Replace a list of members
                if (operation.Path?.ToLowerInvariant() == "members")
                {
                    var ids = GetOperationValueIds(operation.Value);
                    await _groupRepository.UpdateUsersAsync(group.Id, ids);
                    operationHandled = true;
                }
                // Replace group name from path
                else if (operation.Path?.ToLowerInvariant() == "displayname")
                {
                    group.Name = operation.Value.GetString();
                    await _groupService.SaveAsync(group);
                    operationHandled = true;
                }
                // Replace group name from value object
                else if (string.IsNullOrWhiteSpace(operation.Path) &&
                    operation.Value.TryGetProperty("displayName", out var displayNameProperty))
                {
                    group.Name = displayNameProperty.GetString();
                    await _groupService.SaveAsync(group);
                    operationHandled = true;
                }
            }
            // Add a single member
            else if (operation.Op?.ToLowerInvariant() == "add" &&
                !string.IsNullOrWhiteSpace(operation.Path) &&
                operation.Path.ToLowerInvariant().StartsWith("members[value eq "))
            {
                var addId = GetOperationPathId(operation.Path);
                if (addId.HasValue)
                {
                    var orgUserIds = (await _groupRepository.GetManyUserIdsByIdAsync(group.Id)).ToHashSet();
                    orgUserIds.Add(addId.Value);
                    await _groupRepository.UpdateUsersAsync(group.Id, orgUserIds);
                    operationHandled = true;
                }
            }
            // Add a list of members
            else if (operation.Op?.ToLowerInvariant() == "add" &&
                operation.Path?.ToLowerInvariant() == "members")
            {
                var orgUserIds = (await _groupRepository.GetManyUserIdsByIdAsync(group.Id)).ToHashSet();
                foreach (var v in GetOperationValueIds(operation.Value))
                {
                    orgUserIds.Add(v);
                }
                await _groupRepository.UpdateUsersAsync(group.Id, orgUserIds);
                operationHandled = true;
            }
            // Remove a single member
            else if (operation.Op?.ToLowerInvariant() == "remove" &&
                !string.IsNullOrWhiteSpace(operation.Path) &&
                operation.Path.ToLowerInvariant().StartsWith("members[value eq "))
            {
                var removeId = GetOperationPathId(operation.Path);
                if (removeId.HasValue)
                {
                    await _groupService.DeleteUserAsync(group, removeId.Value);
                    operationHandled = true;
                }
            }
            // Remove a list of members
            else if (operation.Op?.ToLowerInvariant() == "remove" &&
                operation.Path?.ToLowerInvariant() == "members")
            {
                var orgUserIds = (await _groupRepository.GetManyUserIdsByIdAsync(group.Id)).ToHashSet();
                foreach (var v in GetOperationValueIds(operation.Value))
                {
                    orgUserIds.Remove(v);
                }
                await _groupRepository.UpdateUsersAsync(group.Id, orgUserIds);
                operationHandled = true;
            }
        }

        if (!operationHandled)
        {
            _logger.LogWarning("Group patch operation not handled: {0} : ",
                string.Join(", ", model.Operations.Select(o => $"{o.Op}:{o.Path}")));
        }

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
        await _deleteGroupCommand.DeleteAsync(group);
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
