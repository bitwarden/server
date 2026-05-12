// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.Text.Json;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Groups.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Scim.Groups.Interfaces;
using Bit.Scim.Models;
using Bit.Scim.Utilities;

namespace Bit.Scim.Groups;

public class PatchGroupCommand : IPatchGroupCommand
{
    private readonly IGroupRepository _groupRepository;
    private readonly IGroupService _groupService;
    private readonly IUpdateGroupCommand _updateGroupCommand;
    private readonly ILogger<PatchGroupCommand> _logger;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly TimeProvider _timeProvider;

    public PatchGroupCommand(
        IGroupRepository groupRepository,
        IGroupService groupService,
        IUpdateGroupCommand updateGroupCommand,
        ILogger<PatchGroupCommand> logger,
        IOrganizationRepository organizationRepository,
        TimeProvider timeProvider)
    {
        _groupRepository = groupRepository;
        _groupService = groupService;
        _updateGroupCommand = updateGroupCommand;
        _logger = logger;
        _organizationRepository = organizationRepository;
        _timeProvider = timeProvider;
    }

    public async Task PatchGroupAsync(Group group, ScimPatchModel model)
    {
        foreach (var operation in model.Operations)
        {
            await HandleOperationAsync(group, operation);
        }
    }

    private async Task HandleOperationAsync(Group group, ScimPatchModel.OperationModel operation)
    {
        switch (operation.Op?.ToLowerInvariant())
        {
            case PatchOps.Replace:
                await HandleReplaceAsync(group, operation);
                break;

            case PatchOps.Add:
                await HandleAddAsync(group, operation);
                break;

            case PatchOps.Remove:
                await HandleRemoveAsync(group, operation);
                break;

            default:
                LogUnhandledOperation(operation);
                break;
        }
    }

    private async Task HandleReplaceAsync(Group group, ScimPatchModel.OperationModel operation)
    {
        switch (operation.Path?.ToLowerInvariant())
        {
            case PatchPaths.Members:
                await ReplaceMembersAsync(group, operation);
                break;

            case PatchPaths.DisplayName:
                await ReplaceDisplayNameAsync(group, operation.Value.GetString());
                break;

            case PatchPaths.ExternalId:
                await HandleExternalIdOperationAsync(group, operation.Value.GetString());
                break;

            case var path when string.IsNullOrWhiteSpace(path):
                await ReplaceFromValueObjectAsync(group, operation);
                break;

            default:
                LogUnhandledOperation(operation);
                break;
        }
    }

    private async Task ReplaceMembersAsync(Group group, ScimPatchModel.OperationModel operation)
    {
        var ids = GetOperationValueIds(operation.Value);
        await _groupRepository.UpdateUsersAsync(group.Id, ids, _timeProvider.GetUtcNow().UtcDateTime);
    }

    private async Task ReplaceFromValueObjectAsync(Group group, ScimPatchModel.OperationModel operation)
    {
        var handled = false;

        if (operation.Value.TryGetProperty("displayName", out var displayNameProperty))
        {
            await ReplaceDisplayNameAsync(group, displayNameProperty.GetString());
            handled = true;
        }
        if (operation.Value.TryGetProperty("externalId", out var externalIdProperty))
        {
            await HandleExternalIdOperationAsync(group, externalIdProperty.GetString());
            handled = true;
        }

        if (!handled)
        {
            LogUnhandledOperation(operation);
        }
    }

    // SCIM-2.0 IdPs that discover an existing group via displayName and notice it lacks their
    // externalId issue an Add op so the link can be persisted.
    private async Task HandleAddAsync(Group group, ScimPatchModel.OperationModel operation)
    {
        switch (operation.Path?.ToLowerInvariant())
        {
            case PatchPaths.ExternalId:
                await HandleExternalIdOperationAsync(group, operation.Value.GetString());
                break;

            case PatchPaths.Members:
                await AddMembersAsync(group, GetOperationValueIds(operation.Value));
                break;

            case var path when
                !string.IsNullOrWhiteSpace(path) &&
                path.StartsWith("members[value eq ", StringComparison.OrdinalIgnoreCase) &&
                TryGetOperationPathId(operation.Path, out var addId):
                await AddMembersAsync(group, [addId]);
                break;

            case var path when string.IsNullOrWhiteSpace(path):
                await AddFromValueObjectAsync(group, operation);
                break;

            default:
                LogUnhandledOperation(operation);
                break;
        }
    }

    private async Task AddFromValueObjectAsync(Group group, ScimPatchModel.OperationModel operation)
    {
        if (operation.Value.TryGetProperty("externalId", out var externalIdProperty))
        {
            await HandleExternalIdOperationAsync(group, externalIdProperty.GetString());
            return;
        }

        LogUnhandledOperation(operation);
    }

    private async Task HandleRemoveAsync(Group group, ScimPatchModel.OperationModel operation)
    {
        switch (operation.Path?.ToLowerInvariant())
        {
            case PatchPaths.Members:
                await RemoveMembersAsync(group, operation);
                break;

            case var path when
                !string.IsNullOrWhiteSpace(path) &&
                path.StartsWith("members[value eq ", StringComparison.OrdinalIgnoreCase) &&
                TryGetOperationPathId(operation.Path, out var removeId):
                await _groupService.DeleteUserAsync(group, removeId, EventSystemUser.SCIM);
                break;

            default:
                LogUnhandledOperation(operation);
                break;
        }
    }

    private async Task RemoveMembersAsync(Group group, ScimPatchModel.OperationModel operation)
    {
        var orgUserIds = (await _groupRepository.GetManyUserIdsByIdAsync(group.Id)).ToHashSet();
        foreach (var v in GetOperationValueIds(operation.Value))
        {
            orgUserIds.Remove(v);
        }
        await _groupRepository.UpdateUsersAsync(group.Id, orgUserIds, _timeProvider.GetUtcNow().UtcDateTime);
    }

    private async Task ReplaceDisplayNameAsync(Group group, string displayName)
    {
        group.Name = displayName;
        var organization = await _organizationRepository.GetByIdAsync(group.OrganizationId);
        if (organization == null)
        {
            throw new NotFoundException();
        }
        await _updateGroupCommand.UpdateGroupAsync(group, organization, EventSystemUser.SCIM);
    }

    private async Task HandleExternalIdOperationAsync(Group group, string newExternalId)
    {
        if (!string.IsNullOrWhiteSpace(newExternalId) && newExternalId.Length > 300)
        {
            throw new BadRequestException("ExternalId cannot exceed 300 characters.");
        }

        if (!string.IsNullOrWhiteSpace(newExternalId))
        {
            var existingGroups = await _groupRepository.GetManyByOrganizationIdAsync(group.OrganizationId);
            if (existingGroups.Any(g => g.Id != group.Id &&
                !string.IsNullOrWhiteSpace(g.ExternalId) &&
                g.ExternalId.Equals(newExternalId, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ConflictException("ExternalId already exists for another group.");
            }
        }

        group.ExternalId = string.IsNullOrWhiteSpace(newExternalId) ? null : newExternalId;
        group.RevisionDate = _timeProvider.GetUtcNow().UtcDateTime;
        await _groupRepository.ReplaceAsync(group);
    }

    private async Task AddMembersAsync(Group group, HashSet<Guid> usersToAdd)
    {
        // Azure Entra ID is known to send redundant "add" requests for each existing member every time any member
        // is removed. To avoid excessive load on the database, we check against the high availability replica and
        // return early if they already exist.
        var groupMembers = await _groupRepository.GetManyUserIdsByIdAsync(group.Id, useReadOnlyReplica: true);
        if (usersToAdd.IsSubsetOf(groupMembers))
        {
            _logger.LogDebug("Ignoring duplicate SCIM request to add members {Members} to group {Group}", usersToAdd, group.Id);
            return;
        }

        await _groupRepository.AddGroupUsersByIdAsync(group.Id, usersToAdd, _timeProvider.GetUtcNow().UtcDateTime);
    }

    private void LogUnhandledOperation(ScimPatchModel.OperationModel operation)
    {
        _logger.LogWarning("Group patch operation not handled: {OperationOp}:{OperationPath}", operation.Op, operation.Path);
    }

    private static HashSet<Guid> GetOperationValueIds(JsonElement objArray)
    {
        var ids = new HashSet<Guid>();
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

    private static bool TryGetOperationPathId(string path, out Guid pathId)
    {
        // Parse Guid from string like: members[value eq "{GUID}"}]
        return Guid.TryParse(path.Substring(18).Replace("\"]", string.Empty), out pathId);
    }
}
