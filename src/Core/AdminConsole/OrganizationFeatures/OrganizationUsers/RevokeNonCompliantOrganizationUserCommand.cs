using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationUserAction;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Requests;
using Bit.Core.AdminConsole.Utilities.Commands;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;

public class RevokeNonCompliantOrganizationUserCommand(IOrganizationUserRepository organizationUserRepository,
    IEventService eventService,
    IHasConfirmedOwnersExceptQuery confirmedOwnersExceptQuery,
    IOrganizationUserValidationService organizationUserValidationService,
    TimeProvider timeProvider) : IRevokeNonCompliantOrganizationUserCommand
{
    public const string ErrorCannotRevokeSelf = "You cannot revoke yourself.";
    public const string ErrorUserAlreadyRevoked = "User is already revoked.";
    public const string ErrorOrgMustHaveAtLeastOneOwner = "Organization must have at least one confirmed owner.";
    public const string ErrorInvalidUsers = "Invalid users.";
    public const string ErrorRequestedByWasNotValid = "Action was performed by an unexpected type.";

    public async Task<CommandResult> RevokeNonCompliantOrganizationUsersAsync(RevokeOrganizationUsersRequest request)
    {
        var validationResult = await ValidateAsync(request);

        if (validationResult.HasErrors)
        {
            return validationResult;
        }

        await organizationUserRepository.RevokeManyAsync(request.OrganizationUsers.Select(x => x.Id), request.RevocationReason);

        var now = timeProvider.GetUtcNow();

        var eventType = MapRevocationReasonToEventType(request.RevocationReason);

        switch (request.ActionPerformedBy)
        {
            case StandardUser:
                await eventService.LogOrganizationUserEventsAsync(
                    request.OrganizationUsers.Select(x => GetRevokedUserEventTuple(x, eventType, now)));
                break;
            case SystemUser { SystemUserType: not null } loggableSystem:
                await eventService.LogOrganizationUserEventsAsync(
                    request.OrganizationUsers.Select(x =>
                        GetRevokedUserEventBySystemUserTuple(x, eventType, loggableSystem.SystemUserType.Value, now)));
                break;
        }

        return validationResult;
    }

    private static EventType MapRevocationReasonToEventType(RevocationReason reason) => reason switch
    {
        RevocationReason.TwoFactorPolicyNonCompliance => EventType.OrganizationUser_Revoked_TwoFactorNonCompliance,
        RevocationReason.SingleOrgPolicyNonCompliance => EventType.OrganizationUser_Revoked_SingleOrganizationNonCompliance,
        _ => EventType.OrganizationUser_Revoked
    };

    private static (OrganizationUserUserDetails organizationUser, EventType eventType, DateTime? time) GetRevokedUserEventTuple(
        OrganizationUserUserDetails organizationUser, EventType eventType, DateTimeOffset dateTimeOffset) =>
        new(organizationUser, eventType, dateTimeOffset.UtcDateTime);

    private static (OrganizationUserUserDetails organizationUser, EventType eventType, EventSystemUser eventSystemUser, DateTime? time) GetRevokedUserEventBySystemUserTuple(
        OrganizationUserUserDetails organizationUser, EventType eventType, EventSystemUser systemUser, DateTimeOffset dateTimeOffset) => new(organizationUser,
        eventType, systemUser, dateTimeOffset.UtcDateTime);

    private async Task<CommandResult> ValidateAsync(RevokeOrganizationUsersRequest request)
    {
        if (!PerformedByIsAnExpectedType(request.ActionPerformedBy))
        {
            return new CommandResult(ErrorRequestedByWasNotValid);
        }

        if (request.ActionPerformedBy is StandardUser user
            && request.OrganizationUsers.Any(x => x.UserId == user.UserId))
        {
            return new CommandResult(ErrorCannotRevokeSelf);
        }

        if (request.OrganizationUsers.Any(x => x.OrganizationId != request.OrganizationId))
        {
            return new CommandResult(ErrorInvalidUsers);
        }

        if (!await confirmedOwnersExceptQuery.HasConfirmedOwnersExceptAsync(
                    request.OrganizationId,
                    request.OrganizationUsers.Select(x => x.Id)))
        {
            return new CommandResult(ErrorOrgMustHaveAtLeastOneOwner);
        }

        var manageErrorsByTarget = await GetManageErrorsAsync(request);

        return request.OrganizationUsers.Aggregate(new CommandResult(), (result, userToRevoke) =>
        {
            if (IsAlreadyRevoked(userToRevoke))
            {
                result.ErrorMessages.Add($"{ErrorUserAlreadyRevoked} Id: {userToRevoke.Id}");
                return result;
            }

            if (manageErrorsByTarget[userToRevoke.Id] is { } manageError)
            {
                result.ErrorMessages.Add(manageError.Message);
                return result;
            }

            return result;
        });
    }

    /// <summary>
    /// Delegates the "can the acting user manage this target's role" decision to
    /// <see cref="IOrganizationUserValidationService"/>'s bulk CanManageAsync overload.
    /// System users (SCIM, Public API) skip the check entirely.
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, Error?>> GetManageErrorsAsync(RevokeOrganizationUsersRequest request)
    {
        var targetsById = request.OrganizationUsers.ToDictionary(u => u.Id, u => (IOrganizationUserRole)u);

        if (request.ActionPerformedBy is not StandardUser standardUser)
        {
            return targetsById.ToDictionary(kvp => kvp.Key, Error? (_) => null);
        }

        var actingUser = standardUser.OrganizationUserType.HasValue
            ? new OrganizationUserRole(standardUser.OrganizationUserType.Value, request.OrganizationId, standardUser.Permissions)
            : null;

        return await organizationUserValidationService.CanManageAsync(
            standardUser.UserId!.Value, actingUser, request.OrganizationId, targetsById)
            ?? new Dictionary<Guid, Error?>();
    }

    private static bool PerformedByIsAnExpectedType(IActingUser entity) => entity is SystemUser or StandardUser;

    private static bool IsAlreadyRevoked(OrganizationUserUserDetails organizationUser) =>
        organizationUser is { Status: OrganizationUserStatusType.Revoked };
}
