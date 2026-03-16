using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.RestoreUser.v1;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.RevokeUser.v1;
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
    private readonly IRestoreOrganizationUserCommand _restoreOrganizationUserCommand;
    private readonly ILogger<PatchUserCommand> _logger;
    private readonly IRevokeOrganizationUserCommand _revokeOrganizationUserCommand;

    public PatchUserCommand(IOrganizationUserRepository organizationUserRepository,
        IRestoreOrganizationUserCommand restoreOrganizationUserCommand,
        ILogger<PatchUserCommand> logger,
        IRevokeOrganizationUserCommand revokeOrganizationUserCommand)
    {
        _organizationUserRepository = organizationUserRepository;
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
            // Replace operations
            if (operation.Op?.ToLowerInvariant() == PatchOps.Replace)
            {
                // Active from path
                if (operation.Path?.ToLowerInvariant() == "active")
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
                else if (operation.Path?.ToLowerInvariant() == PatchPaths.ExternalId)
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
            await _revokeOrganizationUserCommand.RevokeUserAsync(orgUser, EventSystemUser.SCIM);
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
}
