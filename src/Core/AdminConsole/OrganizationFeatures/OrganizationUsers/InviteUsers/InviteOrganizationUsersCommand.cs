using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
using Bit.Core.Models.Commands;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptions.Interface;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Business;
using Bit.Core.Tools.Services;
using Microsoft.Extensions.Logging;
using static Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models.CreateOrganizationUserExtensions;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers;

public static class InviteOrganizationUsersErrorMessages
{
    public const string IssueNotifyingOwnersOfSeatLimitReached = "Error encountered notifying organization owners of seat limit reached.";
    public const string FailedToInviteUsers = "Failed to invite user(s).";
}

public interface IInviteOrganizationUsersCommand
{
    Task<CommandResult<OrganizationUser>> InviteScimOrganizationUserAsync(InviteScimOrganizationUserRequest request);
}

public class InviteOrganizationUsersCommand(IEventService eventService,
    IOrganizationUserRepository organizationUserRepository,
    IInviteUsersValidation inviteUsersValidation,
    IPaymentService paymentService,
    IOrganizationRepository organizationRepository,
    IReferenceEventService referenceEventService,
    ICurrentContext currentContext,
    IApplicationCacheService applicationCacheService,
    IMailService mailService,
    ILogger<InviteOrganizationUsersCommand> logger,
    IUpdateSecretsManagerSubscriptionCommand updateSecretsManagerSubscriptionCommand,
    ISendOrganizationInvitesCommand sendOrganizationInvitesCommand
    ) : IInviteOrganizationUsersCommand
{
    public async Task<CommandResult<OrganizationUser>> InviteScimOrganizationUserAsync(InviteScimOrganizationUserRequest request)
    {
        var result = await InviteOrganizationUsersAsync(InviteOrganizationUsersRequest.Create(request));

        if (result is Failure<IEnumerable<OrganizationUser>> failure)
        {
            return new Failure<OrganizationUser>(failure.ErrorMessage);
        }

        if (result.Value.Any())
        {
            (OrganizationUser User, EventType type, EventSystemUser system, DateTime performedAt) log = (result.Value.First(), EventType.OrganizationUser_Invited, EventSystemUser.SCIM, request.PerformedAt.UtcDateTime);

            await eventService.LogOrganizationUserEventsAsync([log]);
        }

        return new Success<OrganizationUser>(result.Value.FirstOrDefault());
    }

    private async Task<CommandResult<IEnumerable<OrganizationUser>>> InviteOrganizationUsersAsync(InviteOrganizationUsersRequest request)
    {
        var existingEmails = new HashSet<string>(await organizationUserRepository.SelectKnownEmailsAsync(
                request.Organization.OrganizationId, request.Invites.SelectMany(i => i.Emails), false),
            StringComparer.InvariantCultureIgnoreCase);

        var invitesToSend = request.Invites
            .SelectMany(invite => invite.Emails
                .Where(email => !existingEmails.Contains(email))
                .Select(email => OrganizationUserInviteDto.Create(email, invite, request.Organization.OrganizationId))
            );

        if (invitesToSend.Any() is false)
        {
            return new Success<IEnumerable<OrganizationUser>>([]);
        }

        var validationResult = await inviteUsersValidation.ValidateAsync(new InviteUserOrganizationValidationRequest
        {
            Invites = invitesToSend.ToArray(),
            Organization = request.Organization,
            PerformedBy = request.PerformedBy,
            PerformedAt = request.PerformedAt,
            OccupiedPmSeats = await organizationUserRepository.GetOccupiedSeatCountByOrganizationIdAsync(request.Organization.OrganizationId),
            OccupiedSmSeats = await organizationUserRepository.GetOccupiedSmSeatCountByOrganizationIdAsync(request.Organization.OrganizationId)
        });

        if (validationResult is Invalid<InviteUserOrganizationValidationRequest> invalid)
        {
            return new Failure<IEnumerable<OrganizationUser>>(invalid.ErrorMessageString);
        }

        var valid = validationResult as Valid<InviteUserOrganizationValidationRequest>;

        var organizationUserCollection = invitesToSend
            .Select(MapToDataModel(request.PerformedAt))
            .ToArray();

        var organization = await organizationRepository.GetByIdAsync(valid.Value.Organization.OrganizationId);
        try
        {
            await organizationUserRepository.CreateManyAsync(organizationUserCollection);

            await AdjustPasswordManagerSeatsAsync(valid, organization);

            await AdjustSecretsManagerSeatsAsync(valid, organization);

            await SendNotificationsAsync(valid, organization);

            await SendInvitesAsync(organizationUserCollection, organization);

            await PublishEventAsync(valid, organization);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, InviteOrganizationUsersErrorMessages.FailedToInviteUsers);

            await organizationUserRepository.DeleteManyAsync(organizationUserCollection.Select(x => x.User.Id));

            await RevertSecretsManagerChangesAsync(valid, organization);

            await RevertPasswordManagerChangesAsync(valid, organization);

            return new Failure<IEnumerable<OrganizationUser>>(InviteOrganizationUsersErrorMessages.FailedToInviteUsers);
        }

        return new Success<IEnumerable<OrganizationUser>>(organizationUserCollection.Select(x => x.User));
    }

    private async Task RevertPasswordManagerChangesAsync(Valid<InviteUserOrganizationValidationRequest> valid, Organization organization)
    {
        if (valid.Value.PasswordManagerSubscriptionUpdate.SeatsRequiredToAdd < 0)
        {
            await paymentService.AdjustSeatsAsync(organization, valid.Value.Organization.Plan, -valid.Value.PasswordManagerSubscriptionUpdate.SeatsRequiredToAdd);

            organization.Seats = (short?)valid.Value.PasswordManagerSubscriptionUpdate.Seats;

            await organizationRepository.ReplaceAsync(organization);
            await applicationCacheService.UpsertOrganizationAbilityAsync(organization);
        }
    }

    private async Task RevertSecretsManagerChangesAsync(Valid<InviteUserOrganizationValidationRequest> valid, Organization organization)
    {
        if (valid.Value.SecretsManagerSubscriptionUpdate.SeatsRequiredToAdd < 0)
        {
            var updateRevert = new SecretsManagerSubscriptionUpdate(organization, false)
            {
                SmSeats = valid.Value.SecretsManagerSubscriptionUpdate.Seats
            };

            await updateSecretsManagerSubscriptionCommand.UpdateSubscriptionAsync(updateRevert);
        }
    }

    private async Task PublishEventAsync(Valid<InviteUserOrganizationValidationRequest> valid,
        Organization organization) =>
        await referenceEventService.RaiseEventAsync(
            new ReferenceEvent(ReferenceEventType.InvitedUsers, organization, currentContext)
            {
                Users = valid.Value.Invites.Length
            });

    private async Task SendInvitesAsync(IEnumerable<CreateOrganizationUser> users, Organization organization) =>
        await sendOrganizationInvitesCommand.SendInvitesAsync(
            new SendInvitesRequest(
                users.Select(x => x.User),
                organization));

    private async Task SendNotificationsAsync(Valid<InviteUserOrganizationValidationRequest> valid, Organization organization)
    {
        await SendPasswordManagerMaxSeatLimitEmailsAsync(valid, organization);
    }

    private async Task SendPasswordManagerMaxSeatLimitEmailsAsync(Valid<InviteUserOrganizationValidationRequest> valid, Organization organization)
    {
        if (!valid.Value.PasswordManagerSubscriptionUpdate.MaxSeatsReached)
        {
            return;
        }

        try
        {
            var ownerEmails = (await organizationUserRepository
                    .GetManyByMinimumRoleAsync(valid.Value.Organization.OrganizationId, OrganizationUserType.Owner))
                .Select(x => x.Email)
                .Distinct();

            await mailService.SendOrganizationMaxSeatLimitReachedEmailAsync(organization,
                valid.Value.PasswordManagerSubscriptionUpdate.MaxAutoScaleSeats.Value, ownerEmails);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, InviteOrganizationUsersErrorMessages.IssueNotifyingOwnersOfSeatLimitReached);
        }
    }

    private async Task AdjustSecretsManagerSeatsAsync(Valid<InviteUserOrganizationValidationRequest> valid, Organization organization)
    {
        if (valid.Value.SecretsManagerSubscriptionUpdate.SeatsRequiredToAdd <= 0)
        {
            return;
        }

        var subscriptionUpdate = new SecretsManagerSubscriptionUpdate(organization, true)
            .AdjustSeats(valid.Value.SecretsManagerSubscriptionUpdate.SeatsRequiredToAdd);

        await updateSecretsManagerSubscriptionCommand.UpdateSubscriptionAsync(subscriptionUpdate);
    }

    private async Task AdjustPasswordManagerSeatsAsync(Valid<InviteUserOrganizationValidationRequest> valid, Organization organization)
    {
        if (valid.Value.PasswordManagerSubscriptionUpdate.SeatsRequiredToAdd <= 0)
        {
            return;
        }

        // These are the important steps
        await paymentService.AdjustSeatsAsync(organization, valid.Value.Organization.Plan, valid.Value.PasswordManagerSubscriptionUpdate.SeatsRequiredToAdd);

        organization.Seats = (short?)valid.Value.PasswordManagerSubscriptionUpdate.UpdatedSeatTotal;

        await organizationRepository.ReplaceAsync(organization); // could optimize this with only a property update
        await applicationCacheService.UpsertOrganizationAbilityAsync(organization);

        // Do we want to fail if this fails?
        await referenceEventService.RaiseEventAsync(
            new ReferenceEvent(ReferenceEventType.AdjustSeats, organization, currentContext)
            {
                PlanName = valid.Value.Organization.Plan.Name,
                PlanType = valid.Value.Organization.Plan.Type,
                Seats = valid.Value.PasswordManagerSubscriptionUpdate.UpdatedSeatTotal,
                PreviousSeats = valid.Value.PasswordManagerSubscriptionUpdate.Seats
            });
    }
}
