using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Commands;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers;

public interface IInviteOrganizationUsersCommand
{
    Task<CommandResult<OrganizationUser>> InviteScimOrganizationUserAsync(InviteScimOrganizationUserRequest request);
}

public class InviteOrganizationUsersCommand(IEventService eventService,
    IOrganizationUserRepository organizationUserRepository,
    IInviteUsersValidation inviteUsersValidation
    ) : IInviteOrganizationUsersCommand
{
    public async Task<CommandResult<OrganizationUser>> InviteScimOrganizationUserAsync(InviteScimOrganizationUserRequest request)
    {
        var result = await InviteOrganizationUsersAsync(InviteOrganizationUsersRequest.Create(request));

        if (result.Value.Any())
        {
            (OrganizationUser User, EventType type, EventSystemUser system, DateTime performedAt) log = (result.Value.First(), EventType.OrganizationUser_Invited, EventSystemUser.SCIM, request.PerformedAt.UtcDateTime);

            await eventService.LogOrganizationUserEventsAsync([log]);
        }

        return new CommandResult<OrganizationUser>(result.Value.FirstOrDefault());
    }

    private async Task<CommandResult<IEnumerable<OrganizationUser>>> InviteOrganizationUsersAsync(InviteOrganizationUsersRequest request)
    {
        var existingEmails = new HashSet<string>(await organizationUserRepository.SelectKnownEmailsAsync(
                request.Organization.OrganizationId, request.Invites.SelectMany(i => i.Emails), false),
            StringComparer.InvariantCultureIgnoreCase);

        var invitesToSend = request.Invites
            .SelectMany(invite => invite.Emails
                .Where(email => !existingEmails.Contains(email))
                .Select(email => OrganizationUserInviteDto.Create(email, invite))
            );

        // Validate we can add those seats
        var validationResult = await inviteUsersValidation.ValidateAsync(new InviteUserOrganizationValidationRequest
        {
            Invites = invitesToSend.ToArray(),
            Organization = request.Organization,
            PerformedBy = request.PerformedBy,
            PerformedAt = request.PerformedAt,
            OccupiedPmSeats = await organizationUserRepository.GetOccupiedSeatCountByOrganizationIdAsync(request.Organization.OrganizationId),
            OccupiedSmSeats = await organizationUserRepository.GetOccupiedSmSeatCountByOrganizationIdAsync(request.Organization.OrganizationId)
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
