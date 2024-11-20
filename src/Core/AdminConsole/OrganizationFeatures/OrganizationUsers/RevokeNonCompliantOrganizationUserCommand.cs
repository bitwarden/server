using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Requests;
using Bit.Core.Enums;
using Bit.Core.Models.Commands;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;

public class RevokeNonCompliantOrganizationUserCommand(IOrganizationUserRepository organizationUserRepository,
    IEventService eventService,
    IHasConfirmedOwnersExceptQuery confirmedOwnersExceptQuery,
    TimeProvider timeProvider) : IRevokeNonCompliantOrganizationUserCommand
{
    public const string ErrorCannotRevokeSelf = "You cannot revoke yourself.";
    public const string ErrorOnlyOwnersCanRevokeOtherOwners = "Only owners can revoke other owners.";
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

        await organizationUserRepository.RevokeManyByIdAsync(request.OrganizationUsers.Select(x => x.Id));

        var now = timeProvider.GetUtcNow();

        switch (request.ActionPerformedBy)
        {
            case StandardUser:
                await eventService.LogOrganizationUserEventsAsync(
                    request.OrganizationUsers.Select(x => GetRevokedUserEventTuple(x, now)));
                break;
            case SystemUser { SystemUserType: not null } loggableSystem:
                await eventService.LogOrganizationUserEventsAsync(
                    request.OrganizationUsers.Select(x =>
                        GetRevokedUserEventBySystemUserTuple(x, loggableSystem.SystemUserType.Value, now)));
                break;
        }

        return validationResult;
    }

    private static (OrganizationUserUserDetails organizationUser, EventType eventType, DateTime? time) GetRevokedUserEventTuple(
        OrganizationUserUserDetails organizationUser, DateTimeOffset dateTimeOffset) =>
        new(organizationUser, EventType.OrganizationUser_Revoked, dateTimeOffset.UtcDateTime);

    private static (OrganizationUserUserDetails organizationUser, EventType eventType, EventSystemUser eventSystemUser, DateTime? time) GetRevokedUserEventBySystemUserTuple(
        OrganizationUserUserDetails organizationUser, EventSystemUser systemUser, DateTimeOffset dateTimeOffset) => new(organizationUser,
        EventType.OrganizationUser_Revoked, systemUser, dateTimeOffset.UtcDateTime);

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

        return request.OrganizationUsers.Aggregate(new CommandResult(), (result, userToRevoke) =>
        {
            if (IsAlreadyRevoked(userToRevoke))
            {
                result.ErrorMessages.Add($"{ErrorUserAlreadyRevoked} Id: {userToRevoke.Id}");
                return result;
            }

            if (NonOwnersCannotRevokeOwners(userToRevoke, request.ActionPerformedBy))
            {
                result.ErrorMessages.Add($"{ErrorOnlyOwnersCanRevokeOtherOwners}");
                return result;
            }

            return result;
        });
    }

    private static bool PerformedByIsAnExpectedType(IActingUser entity) => entity is SystemUser or StandardUser;

    private static bool IsAlreadyRevoked(OrganizationUserUserDetails organizationUser) =>
        organizationUser is { Status: OrganizationUserStatusType.Revoked };

    private static bool NonOwnersCannotRevokeOwners(OrganizationUserUserDetails organizationUser,
        IActingUser actingUser) =>
        actingUser is StandardUser { IsOrganizationOwnerOrProvider: false } && organizationUser.Type == OrganizationUserType.Owner;
}
