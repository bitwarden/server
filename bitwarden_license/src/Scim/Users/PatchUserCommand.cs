using System.Text.Json;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.RestoreUser.v1;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.RevokeUser.v1;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Scim.Models;
using Bit.Scim.Users.Interfaces;
using Bit.Scim.Utilities;

namespace Bit.Scim.Users;

public class PatchUserCommand : IPatchUserCommand
{
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IGroupRepository _groupRepository;
    private readonly IRestoreOrganizationUserCommand _restoreOrganizationUserCommand;
    private readonly ILogger<PatchUserCommand> _logger;
    private readonly IRevokeOrganizationUserCommand _revokeOrganizationUserCommand;

    public PatchUserCommand(IOrganizationUserRepository organizationUserRepository,
        IGroupRepository groupRepository,
        IRestoreOrganizationUserCommand restoreOrganizationUserCommand,
        ILogger<PatchUserCommand> logger,
        IRevokeOrganizationUserCommand revokeOrganizationUserCommand)
    {
        _organizationUserRepository = organizationUserRepository;
        _groupRepository = groupRepository;
        _restoreOrganizationUserCommand = restoreOrganizationUserCommand;
        _logger = logger;
        _revokeOrganizationUserCommand = revokeOrganizationUserCommand;
    }

    public async Task PatchUserAsync(Guid organizationId, Guid id, ScimPatchModel model)
    {
        var orgUser = await _organizationUserRepository.GetByIdAsync(id);
        if (orgUser == null || orgUser.OrganizationId != organizationId)
        {
            throw new NotFoundException("User not found.");
        }

        var operationHandled = false;
        foreach (var operation in model.Operations)
        {
            var opLower = operation.Op?.ToLowerInvariant();
            var pathLower = operation.Path?.ToLowerInvariant();

            // Handle add/remove/replace operations on groups path
            if (pathLower == "groups" && (opLower == PatchOps.Add || opLower == PatchOps.Remove || opLower == PatchOps.Replace))
            {
                if (opLower == PatchOps.Add)
                {
                    await HandleAddGroupOperationAsync(orgUser, operation.Value);
                    operationHandled = true;
                }
                else if (opLower == PatchOps.Remove)
                {
                    await HandleRemoveGroupOperationAsync(orgUser, operation.Value);
                    operationHandled = true;
                }
                else if (opLower == PatchOps.Replace)
                {
                    await HandleReplaceGroupsOperationAsync(orgUser, operation.Value);
                    operationHandled = true;
                }
            }
            // Replace operations (existing logic)
            else if (opLower == PatchOps.Replace)
            {
                // Active from path
                if (pathLower == "active")
                {
                    var active = operation.Value.ToString()?.ToLowerInvariant();
                    var handled = await HandleActiveOperationAsync(orgUser, active == "true");
                    if (!operationHandled)
                    {
                        operationHandled = handled;
                    }
                    // Re-fetch to pick up status changes persisted by restore/revoke
                    if (handled)
                    {
                        orgUser = await _organizationUserRepository.GetByIdAsync(orgUser.Id)
                            ?? throw new NotFoundException("User not found.");
                    }
                }
                // ExternalId from path
                else if (pathLower == PatchPaths.ExternalId)
                {
                    var newExternalId = operation.Value.GetString();
                    await HandleExternalIdOperationAsync(orgUser, newExternalId);
                    operationHandled = true;
                }
                // Value object with no path — check for each supported property independently
                else if (string.IsNullOrWhiteSpace(operation.Path))
                {
                    if (operation.Value.TryGetProperty("active", out var activeProperty))
                    {
                        var handled = await HandleActiveOperationAsync(orgUser, activeProperty.GetBoolean());
                        if (!operationHandled)
                        {
                            operationHandled = handled;
                        }
                        // Re-fetch to pick up status changes persisted by restore/revoke
                        if (handled)
                        {
                            orgUser = await _organizationUserRepository.GetByIdAsync(orgUser.Id)
                                ?? throw new NotFoundException("User not found.");
                        }
                    }
                    if (operation.Value.TryGetProperty("externalId", out var externalIdProperty))
                    {
                        var newExternalId = externalIdProperty.GetString();
                        await HandleExternalIdOperationAsync(orgUser, newExternalId);
                        operationHandled = true;
                    }
                }
            }
        }

        if (!operationHandled)
        {
            _logger.LogWarning("User patch operation not handled: {operation} : ",
                string.Join(", ", model.Operations.Select(o => $"{o.Op}:{o.Path}")));
        }
    }

    private async Task<bool> HandleActiveOperationAsync(Core.Entities.OrganizationUser orgUser, bool active)
    {
        if (active && orgUser.Status == OrganizationUserStatusType.Revoked)
        {
            await _restoreOrganizationUserCommand.RestoreUserAsync(orgUser, EventSystemUser.SCIM);
            return true;
        }
        else if (!active && orgUser.Status != OrganizationUserStatusType.Revoked)
        {
            await _revokeOrganizationUserCommand.RevokeUserAsync(orgUser, EventSystemUser.SCIM, RevocationReason.Manual);
            return true;
        }
        return false;
    }

    private async Task HandleExternalIdOperationAsync(Core.Entities.OrganizationUser orgUser, string? newExternalId)
    {
        // Validate max length (300 chars per OrganizationUser.cs line 59)
        if (!string.IsNullOrWhiteSpace(newExternalId) && newExternalId.Length > 300)
        {
            throw new BadRequestException("ExternalId cannot exceed 300 characters.");
        }

        // Check for duplicate externalId (same validation as PostUserCommand.cs)
        if (!string.IsNullOrWhiteSpace(newExternalId))
        {
            var existingUsers = await _organizationUserRepository.GetManyDetailsByOrganizationAsync(orgUser.OrganizationId);
            if (existingUsers.Any(u => u.Id != orgUser.Id &&
                !string.IsNullOrWhiteSpace(u.ExternalId) &&
                u.ExternalId.Equals(newExternalId, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ConflictException("ExternalId already exists for another user.");
            }
        }

        orgUser.ExternalId = newExternalId;
        await _organizationUserRepository.ReplaceAsync(orgUser);
    }

    private async Task HandleAddGroupOperationAsync(Core.Entities.OrganizationUser orgUser, JsonElement value)
    {
        var groupIds = ExtractGroupIds(value);
        await ValidateGroupsAsync(orgUser, groupIds, "Cannot add user to a group from a different organization.");
        var current = (await _organizationUserRepository.GetManyGroupIdsByUserIdAsync(orgUser.Id)).ToHashSet();
        foreach (var groupId in groupIds.Where(g => !current.Contains(g)))
        {
            await _groupRepository.AddGroupUsersByIdAsync(groupId, new[] { orgUser.Id }, DateTime.UtcNow);
        }
    }

    private async Task HandleRemoveGroupOperationAsync(Core.Entities.OrganizationUser orgUser, JsonElement value)
    {
        var groupIds = ExtractGroupIds(value);
        await ValidateGroupsAsync(orgUser, groupIds, "Cannot remove user from a group in a different organization.");
        foreach (var groupId in groupIds)
        {
            await _groupRepository.DeleteUserAsync(groupId, orgUser.Id, DateTime.UtcNow);
        }
    }

    private async Task HandleReplaceGroupsOperationAsync(Core.Entities.OrganizationUser orgUser, JsonElement value)
    {
        var desired = ExtractGroupIds(value).ToHashSet();
        await ValidateGroupsAsync(orgUser, desired, "Cannot modify user's membership in a group from a different organization.");

        var current = (await _organizationUserRepository.GetManyGroupIdsByUserIdAsync(orgUser.Id)).ToHashSet();
        var revisionDate = DateTime.UtcNow;
        foreach (var groupId in current.Except(desired))
        {
            await _groupRepository.DeleteUserAsync(groupId, orgUser.Id, revisionDate);
        }
        foreach (var groupId in desired.Except(current))
        {
            await _groupRepository.AddGroupUsersByIdAsync(groupId, new[] { orgUser.Id }, revisionDate);
        }
    }

    private async Task ValidateGroupsAsync(Core.Entities.OrganizationUser orgUser, IEnumerable<Guid> groupIds, string wrongOrgMessage)
    {
        foreach (var groupId in groupIds)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null)
            {
                throw new NotFoundException($"Group with id {groupId} not found.");
            }
            if (group.OrganizationId != orgUser.OrganizationId)
            {
                throw new BadRequestException(wrongOrgMessage);
            }
        }
    }

    private static List<Guid> ExtractGroupIds(JsonElement value)
    {
        var items = value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().ToList() : new List<JsonElement> { value };
        var ids = new List<Guid>();
        foreach (var item in items)
        {
            if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("value", out var v)
                && Guid.TryParse(v.GetString(), out var id))
            {
                ids.Add(id);
            }
            else
            {
                throw new BadRequestException("Invalid group reference format. Expected {\"value\": \"<groupId>\"} or an array of them.");
            }
        }
        if (ids.Count == 0)
        {
            throw new BadRequestException("Invalid groups array format. Expected [{\"value\": \"<groupId>\"}, ...]");
        }
        return ids;
    }
}
