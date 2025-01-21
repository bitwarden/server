using System.Text.Json;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Groups.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Scim.Groups.Interfaces;
using Bit.Scim.Models;
using Bit.Scim.Utilities;

namespace Bit.Scim.Groups;

public class PatchGroupCommandvNext : IPatchGroupCommandvNext
{
    private readonly IGroupRepository _groupRepository;
    private readonly IGroupService _groupService;
    private readonly IUpdateGroupCommand _updateGroupCommand;
    private readonly ILogger<PatchGroupCommandvNext> _logger;

    public PatchGroupCommandvNext(
        IGroupRepository groupRepository,
        IGroupService groupService,
        IUpdateGroupCommand updateGroupCommand,
        ILogger<PatchGroupCommandvNext> logger)
    {
        _groupRepository = groupRepository;
        _groupService = groupService;
        _updateGroupCommand = updateGroupCommand;
        _logger = logger;
    }

    public async Task PatchGroupAsync(Organization organization, Guid id, ScimPatchModel model)
    {
        var group = await _groupRepository.GetByIdAsync(id);
        if (group == null || group.OrganizationId != organization.Id)
        {
            throw new NotFoundException("Group not found.");
        }

        foreach (var operation in model.Operations)
        {
            await HandleOperation(organization, group, operation);
        }
    }

    private async Task HandleOperation(Organization organization, Group group, ScimPatchModel.OperationModel operation)
    {
        switch (operation.Op?.ToLowerInvariant())
        {
            // Replace a list of members
            case PatchOps.Replace when operation.Path?.ToLowerInvariant() == PatchPaths.Members:
                {
                    var ids = GetOperationValueIds(operation.Value);
                    await _groupRepository.UpdateUsersAsync(group.Id, ids);
                    break;
                }

            // Replace group name from path
            case PatchOps.Replace when operation.Path?.ToLowerInvariant() == PatchPaths.DisplayName:
                {
                    group.Name = operation.Value.GetString();
                    await _updateGroupCommand.UpdateGroupAsync(group, organization, EventSystemUser.SCIM);
                    break;
                }

            // Replace group name from value object
            case PatchOps.Replace when
                string.IsNullOrWhiteSpace(operation.Path) &&
                operation.Value.TryGetProperty("displayName", out var displayNameProperty):
                {
                    group.Name = displayNameProperty.GetString();
                    await _updateGroupCommand.UpdateGroupAsync(group, organization, EventSystemUser.SCIM);
                    break;
                }

            // Add a single member
            case PatchOps.Add when
                !string.IsNullOrWhiteSpace(operation.Path) &&
                operation.Path.ToLowerInvariant().StartsWith("members[value eq ") &&
                TryGetOperationPathId(operation.Path, out var addId):
                {
                    var orgUserIds = (await _groupRepository.GetManyUserIdsByIdAsync(group.Id)).ToHashSet();
                    orgUserIds.Add(addId);
                    await _groupRepository.UpdateUsersAsync(group.Id, orgUserIds);
                    break;
                }

            // Add a list of members
            case PatchOps.Add when
                operation.Path?.ToLowerInvariant() == PatchPaths.Members:
                {
                    var orgUserIds = (await _groupRepository.GetManyUserIdsByIdAsync(group.Id)).ToHashSet();
                    foreach (var v in GetOperationValueIds(operation.Value))
                    {
                        orgUserIds.Add(v);
                    }
                    await _groupRepository.UpdateUsersAsync(group.Id, orgUserIds);
                    break;
                }

            // Remove a single member
            case PatchOps.Remove when
                !string.IsNullOrWhiteSpace(operation.Path) &&
                operation.Path.ToLowerInvariant().StartsWith("members[value eq ") &&
                TryGetOperationPathId(operation.Path, out var removeId):
                {
                    await _groupService.DeleteUserAsync(group, removeId, EventSystemUser.SCIM);
                    break;
                }

            // Remove a list of members
            case PatchOps.Remove when
                operation.Path?.ToLowerInvariant() == PatchPaths.Members:
                {
                    var orgUserIds = (await _groupRepository.GetManyUserIdsByIdAsync(group.Id)).ToHashSet();
                    foreach (var v in GetOperationValueIds(operation.Value))
                    {
                        orgUserIds.Remove(v);
                    }
                    await _groupRepository.UpdateUsersAsync(group.Id, orgUserIds);
                    break;
                }

            default:
                {
                    _logger.LogWarning("Group patch operation not handled: {OperationOp}:{OperationPath}", operation.Op, operation.Path);
                    break;
                }
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

    private bool TryGetOperationPathId(string path, out Guid pathId)
    {
        // Parse Guid from string like: members[value eq "{GUID}"}]
        return Guid.TryParse(path.Substring(18).Replace("\"]", string.Empty), out pathId);
    }
}
