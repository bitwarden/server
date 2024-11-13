using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Requests;
using Bit.Core.AdminConsole.OrganizationFeatures.Shared;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;

public interface IRevokeNonCompliantOrganizationUserCommand
{
    Task<CommandResult> RevokeNonCompliantOrganizationUsersAsync(RevokeOrganizationUsers request);
}

public class RevokeNonCompliantOrganizationUserCommand(IOrganizationUserRepository organizationUserRepository,
    IEventService eventService,
    IHasConfirmedOwnersExceptQuery confirmedOwnersExceptQuery,
    TimeProvider timeProvider) : IRevokeNonCompliantOrganizationUserCommand
{
    public const string CannotRevokeSelfMessage = "You cannot revoke yourself.";
    public const string OnlyOwnersCanRevokeOtherOwners = "Only owners can revoke other owners.";
    public const string UserAlreadyRevoked = "User is already revoked.";
    public const string OrgMustHaveAtLeastOneOwner = "Organization must have at least one confirmed owner.";
    public const string InvalidUsers = "Invalid users.";
    public const string RequestedByWasNotValid = "Action was performed by an unexpected type.";

    public async Task<CommandResult> RevokeNonCompliantOrganizationUsersAsync(RevokeOrganizationUsers request)
    {
        var validationResult = await ValidateAsync(request);

        if (validationResult.HasErrors)
        {
            return validationResult;
        }

        await organizationUserRepository.SetOrganizationUsersStatusAsync(request.OrganizationUsers.Select(x => x.Id),
            OrganizationUserStatusType.Revoked);

        if (request.ActionPerformedBy.GetType() == typeof(StandardUser))
        {
            await eventService.LogOrganizationUserEventsAsync(
                request.OrganizationUsers.Select(x => GetRevokedUserEventTuple(x, timeProvider.GetUtcNow())));
        }
        else if (request.ActionPerformedBy is SystemUser { SystemUserType: not null } loggableSystem)
        {
            await eventService.LogOrganizationUserEventsAsync(
                request.OrganizationUsers.Select(x =>
                    GetRevokedUserEventBySystemUserTuple(x, loggableSystem.SystemUserType.Value,
                        timeProvider.GetUtcNow())));
        }

        return validationResult;
    }

    private static (OrganizationUserUserDetails organizationUser, EventType eventType, DateTime? time) GetRevokedUserEventTuple(
        OrganizationUserUserDetails organizationUser, DateTimeOffset dateTimeOffset) => new(organizationUser,
        EventType.OrganizationUser_Revoked, dateTimeOffset.UtcDateTime);

    private static (OrganizationUserUserDetails organizationUser, EventType eventType, EventSystemUser eventSystemUser, DateTime? time) GetRevokedUserEventBySystemUserTuple(
        OrganizationUserUserDetails organizationUser, EventSystemUser systemUser, DateTimeOffset dateTimeOffset) => new(organizationUser,
        EventType.OrganizationUser_Revoked, systemUser, dateTimeOffset.UtcDateTime);

    private async Task<CommandResult> ValidateAsync(RevokeOrganizationUsers request)
    {
        if (!PerformedByIsAnExpectedType(request.ActionPerformedBy))
        {
            return new CommandResult([RequestedByWasNotValid]);
        }

        if (request.ActionPerformedBy is StandardUser loggableUser
            && request.OrganizationUsers.Any(x => x.UserId == loggableUser.UserId))
        {
            return new CommandResult([CannotRevokeSelfMessage]);
        }

        if (request.OrganizationUsers.Any(x => x.OrganizationId != request.OrganizationId))
        {
            return new CommandResult([InvalidUsers]);
        }

        if (!await confirmedOwnersExceptQuery.HasConfirmedOwnersExceptAsync(
                    request.OrganizationId,
                    request.OrganizationUsers.Select(x => x.Id)))
        {
            return new CommandResult([OrgMustHaveAtLeastOneOwner]);
        }

        return request.OrganizationUsers.Aggregate(new CommandResult(), (result, userToRevoke) =>
        {
            if (IsAlreadyRevoked(userToRevoke))
            {
                result.ErrorMessages.Add($"{UserAlreadyRevoked} Id: {userToRevoke.Id}");
                return result;
            }

            if (!IsNonOwnerRevokingAnOwner(userToRevoke, request.ActionPerformedBy))
            {
                result.ErrorMessages.Add($"{OnlyOwnersCanRevokeOtherOwners}");
                return result;
            }

            return result;
        });
    }

    private static bool PerformedByIsAnExpectedType(IActingUser entity) => entity is SystemUser or StandardUser;

    private static bool IsAlreadyRevoked(OrganizationUserUserDetails organizationUser) =>
        organizationUser is { Status: OrganizationUserStatusType.Revoked };

    private static bool IsNonOwnerRevokingAnOwner(OrganizationUserUserDetails organizationUser,
        IActingUser requestingUser) => requestingUser is StandardUser standardUser &&
                                           !IsActingUserAllowedToRevokeOwner(organizationUser, standardUser);

    private static bool IsActingUserAllowedToRevokeOwner(OrganizationUserUserDetails organizationUser,
        StandardUser requestingOrganizationUser) => organizationUser is { Type: OrganizationUserType.Owner }
                                                    && requestingOrganizationUser.IsOrganizationOwner;
}
