using System.Text.Json;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.Groups.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Scim.Groups.Interfaces;
using Bit.Scim.Models;

namespace Bit.Scim.Groups;

public class PatchGroupCommand : IPatchGroupCommand
{
    private readonly IGroupRepository _groupRepository;
    private readonly IUpdateGroupCommand _updateGroupCommand;
    private readonly ILogger<PatchGroupCommand> _logger;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IEventService _eventService;

    public PatchGroupCommand(
        IGroupRepository groupRepository,
        IUpdateGroupCommand updateGroupCommand,
        ILogger<PatchGroupCommand> logger,
        IOrganizationUserRepository organizationUserRepository,
        IEventService eventService)
    {
        _groupRepository = groupRepository;
        _updateGroupCommand = updateGroupCommand;
        _logger = logger;
        _organizationUserRepository = organizationUserRepository;
        _eventService = eventService;
    }

    public async Task PatchGroupAsync(Organization organization, Guid id, ScimPatchModel model)
    {
        var group = await _groupRepository.GetByIdAsync(id);
        if (group == null || group.OrganizationId != organization.Id)
        {
            throw new NotFoundException("Group not found.");
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
                    await _updateGroupCommand.UpdateGroupAsync(group, organization, EventSystemUser.SCIM);
                    operationHandled = true;
                }
                // Replace group name from value object
                else if (string.IsNullOrWhiteSpace(operation.Path) &&
                    operation.Value.TryGetProperty("displayName", out var displayNameProperty))
                {
                    group.Name = displayNameProperty.GetString();
                    await _updateGroupCommand.UpdateGroupAsync(group, organization, EventSystemUser.SCIM);
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
                var organizationUserId = GetOperationPathId(operation.Path);
                if (organizationUserId.HasValue)
                {
                    var groupUser = await _groupRepository.GetGroupUserByGroupIdOrganizationUserId(group.Id, organizationUserId.Value);
                    if (groupUser == null)
                    {
                        throw new NotFoundException();
                    }

                    var orgUser = await _organizationUserRepository.GetByIdAsync(groupUser.OrganizationUserId);
                    await _groupRepository.DeleteUserAsync(groupUser.GroupId, groupUser.OrganizationUserId);
                    await _eventService.LogOrganizationUserEventAsync(orgUser, EventType.OrganizationUser_UpdatedGroups,
                        EventSystemUser.SCIM);

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
}
