using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Authorization;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Requests;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Validation;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Commands;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;

public interface IInviteOrganizationUsersCommand
{
    Task<CommandResult<OrganizationUser>> InviteOrganizationUserAsync(InviteOrganizationUserRequest request);
    Task<CommandResult<IEnumerable<OrganizationUser>>> InviteOrganizationUserListAsync(InviteOrganizationUsersRequest request);
    Task<CommandResult<OrganizationUser>> InviteScimOrganizationUserAsync(InviteScimOrganizationUserRequest request);
    Task<CommandResult<OrganizationUser>> InvitePublicApiOrganizationUserAsync(InviteOrganizationUserRequest request);
}

public class InviteOrganizationUsersCommand(IEventService eventService,
    IAuthorizationService authorizationService,
    IOrganizationUserRepository organizationUserRepository,
    ICurrentContext currentContext,
    IInviteUsersValidation inviteUsersValidation
    ) : IInviteOrganizationUsersCommand
{
    public async Task<CommandResult<OrganizationUser>> InviteOrganizationUserAsync(InviteOrganizationUserRequest request) =>
        new((await InviteOrganizationUserListAsync(InviteOrganizationUsersRequest.Create(request))).Data.FirstOrDefault());

    public async Task<CommandResult<IEnumerable<OrganizationUser>>> InviteOrganizationUserListAsync(InviteOrganizationUsersRequest request)
    {
        var authorized = await authorizationService.AuthorizeAsync(currentContext.HttpContext.User, request,
            InviteOrganizationUserOperations.Invite);

        if (authorized.Failure is not null)
        {
            throw new UnauthorizedAccessException(authorized.Failure.FailureReasons.ToString());
        }

        var result = await InviteOrganizationUsersAsync(request);

        IEnumerable<(OrganizationUser, EventType, DateTime?)> log = result.Data.Select(invite =>
            (invite, EventType.OrganizationUser_Invited, (DateTime?)request.PerformedAt.UtcDateTime));

        await eventService.LogOrganizationUserEventsAsync(log);

        return result;
    }

    public async Task<CommandResult<OrganizationUser>> InviteScimOrganizationUserAsync(InviteScimOrganizationUserRequest request)
    {
        var result = await InviteOrganizationUsersAsync(InviteOrganizationUsersRequest.Create(request));

        if (result.Data.Any())
        {
            (OrganizationUser User, EventType type, EventSystemUser system, DateTime performedAt) log = (result.Data.First(), EventType.OrganizationUser_Invited, EventSystemUser.SCIM, request.PerformedAt.UtcDateTime);

            await eventService.LogOrganizationUserEventsAsync([log]);
        }

        return new CommandResult<OrganizationUser>(result.Data.FirstOrDefault());
    }

    public async Task<CommandResult<OrganizationUser>> InvitePublicApiOrganizationUserAsync(InviteOrganizationUserRequest request)
    {
        var result = await InviteOrganizationUsersAsync(InviteOrganizationUsersRequest.Create(request));

        if (result.Data.Any())
        {
            (OrganizationUser User, EventType type, DateTime performedAt) log = (result.Data.First(), EventType.OrganizationUser_Invited, request.PerformedAt.UtcDateTime);

            await eventService.LogOrganizationUserEventsAsync([log]);
        }

        return new CommandResult<OrganizationUser>(result.Data.FirstOrDefault());
    }

    private async Task<CommandResult<IEnumerable<OrganizationUser>>> InviteOrganizationUsersAsync(InviteOrganizationUsersRequest request)
    {
        var existingEmails = new HashSet<string>(await organizationUserRepository.SelectKnownEmailsAsync(
                request.Organization.OrganizationId, request.Invites.SelectMany(i => i.Emails), false),
            StringComparer.InvariantCultureIgnoreCase);

        var invitesToSend = request.Invites
            .SelectMany(invite => invite.Emails
                .Where(email => !existingEmails.Contains(email))
                .Select(email => OrganizationUserForInvite.Create(email, invite))
            );

        // Validate we can add those seats
        var validationResult = await inviteUsersValidation.ValidateAsync(new InviteOrganizationUserRefined
        {
            Invites = invitesToSend.ToArray(),
            Organization = request.Organization,
            PerformedBy = request.PerformedBy,
            PerformedAt = request.PerformedAt,
            OccupiedPmSeats = await organizationUserRepository.GetOccupiedSeatCountByOrganizationIdAsync(request.Organization.Id),
            OccupiedSmSeats = await organizationUserRepository.GetOccupiedSmSeatCountByOrganizationIdAsync(request.Organization.Id)
        });

        try
        {
            // save organization users
            // org users
            // collections
            // groups

            // save new seat totals
            // password manager
            // secrets manager
            // update stripe

            // send invites

            // notify owners
            // seats added
            // autoscaling
            // max seat limit has been reached

            // publish events
            // Reference events

            // update cache
        }
        catch (Exception)
        {
            // rollback saves
            // remove org users
            // remove collections
            // remove groups
            // correct stripe
        }

        return null;
    }
}

public class OrganizationUserForInvite
{
    public string Email { get; private init; } = string.Empty;
    public Guid[] AccessibleCollections { get; private init; } = [];
    public string ExternalId { get; private init; } = string.Empty;
    public Permissions Permissions { get; private init; } = new();
    public OrganizationUserType Type { get; private init; } = OrganizationUserType.User;
    public bool AccessSecretsManager { get; private init; }

    public static OrganizationUserForInvite Create(string email, OrganizationUserInvite invite)
    {
        return new OrganizationUserForInvite
        {
            Email = email,
            AccessibleCollections = invite.AccessibleCollections,
            ExternalId = invite.ExternalId,
            Type = invite.Type,
            Permissions = invite.Permissions,
            AccessSecretsManager = invite.AccessSecretsManager
        };
    }
}

public record InviteOrganizationUserRefined
{
    public OrganizationUserForInvite[] Invites { get; init; } = [];
    public OrganizationDto Organization { get; init; }
    public Guid PerformedBy { get; init; }
    public DateTimeOffset PerformedAt { get; init; }
    public int OccupiedPmSeats { get; init; }
    public int OccupiedSmSeats { get; init; }
}

public static class Functions
{
    public static Func<CollectionAccessSelection, bool> ValidateCollectionConfiguration => collectionAccessSelection =>
        collectionAccessSelection.Manage && (collectionAccessSelection.ReadOnly || collectionAccessSelection.HidePasswords);
}
