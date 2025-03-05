using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Interfaces;
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

    public const string IssueNotifyingOwnersOfSeatLimitReached = "Error encountered notifying organization owners of seat limit reached.";
    public const string FailedToInviteUsers = "Failed to invite user(s).";

    public async Task<CommandResult<ScimInviteOrganizationUsersResponse>> InviteScimOrganizationUserAsync(InviteScimOrganizationUserRequest request)
    {
        var result = await InviteOrganizationUsersAsync(new InviteOrganizationUsersRequest(request));

        if (result is Failure<IEnumerable<OrganizationUser>> failure)
        {
            return new Failure<ScimInviteOrganizationUsersResponse>(failure.ErrorMessage);
        }

        if (result.Value.Any())
        {
            await eventService.LogOrganizationUserEventAsync<IOrganizationUser>(result.Value.First(), EventType.OrganizationUser_Invited, EventSystemUser.SCIM, request.PerformedAt.UtcDateTime);
        }

        return new Success<ScimInviteOrganizationUsersResponse>(new ScimInviteOrganizationUsersResponse
        {
            InvitedUser = result.Value.FirstOrDefault()
        });
    }

    private async Task<CommandResult<IEnumerable<OrganizationUser>>> InviteOrganizationUsersAsync(InviteOrganizationUsersRequest request)
    {
        var existingEmails = new HashSet<string>(await organizationUserRepository.SelectKnownEmailsAsync(
                request.InviteOrganization.OrganizationId, request.Invites.SelectMany(i => i.Emails), false),
            StringComparer.InvariantCultureIgnoreCase);

        var invitesToSend = request.Invites
            .SelectMany(invite => invite.Emails
                .Where(email => !existingEmails.Contains(email))
                .Select(email => OrganizationUserInviteDto.Create(email, invite, request.InviteOrganization.OrganizationId))
            ).ToArray();

        if (invitesToSend.Length == 0)
        {
            return new Success<IEnumerable<OrganizationUser>>([]);
        }

        var validationResult = await inviteUsersValidation.ValidateAsync(new InviteUserOrganizationValidationRequest
        {
            Invites = invitesToSend.ToArray(),
            InviteOrganization = request.InviteOrganization,
            PerformedBy = request.PerformedBy,
            PerformedAt = request.PerformedAt,
            OccupiedPmSeats = await organizationUserRepository.GetOccupiedSeatCountByOrganizationIdAsync(request.InviteOrganization.OrganizationId),
            OccupiedSmSeats = await organizationUserRepository.GetOccupiedSmSeatCountByOrganizationIdAsync(request.InviteOrganization.OrganizationId)
        });

        if (validationResult is Invalid<InviteUserOrganizationValidationRequest> invalid)
        {
            return new Failure<IEnumerable<OrganizationUser>>(invalid.ErrorMessageString);
        }

        var validatedRequest = validationResult as Valid<InviteUserOrganizationValidationRequest>;

        var organizationUserCollection = invitesToSend
            .Select(MapToDataModel(request.PerformedAt))
            .ToArray();

        var organization = await organizationRepository.GetByIdAsync(validatedRequest!.Value.InviteOrganization.OrganizationId);
        try
        {
            await organizationUserRepository.CreateManyAsync(organizationUserCollection);

            await AdjustPasswordManagerSeatsAsync(validatedRequest, organization);

            await AdjustSecretsManagerSeatsAsync(validatedRequest, organization);

            await SendAdditionalEmailsAsync(validatedRequest, organization);

            await SendInvitesAsync(organizationUserCollection, organization);

            await PublishReferenceEventAsync(validatedRequest, organization);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, FailedToInviteUsers);

            await organizationUserRepository.DeleteManyAsync(organizationUserCollection.Select(x => x.OrganizationUser.Id));

            await RevertSecretsManagerChangesAsync(validatedRequest, organization);

            await RevertPasswordManagerChangesAsync(validatedRequest, organization);

            return new Failure<IEnumerable<OrganizationUser>>(FailedToInviteUsers);
        }

        return new Success<IEnumerable<OrganizationUser>>(organizationUserCollection.Select(x => x.OrganizationUser));
    }

    private async Task RevertPasswordManagerChangesAsync(Valid<InviteUserOrganizationValidationRequest> valid, Organization organization)
    {
        if (valid.Value.PasswordManagerSubscriptionUpdate.SeatsRequiredToAdd > 0)
        {
            await paymentService.AdjustSeatsAsync(organization, valid.Value.InviteOrganization.Plan, -valid.Value.PasswordManagerSubscriptionUpdate.SeatsRequiredToAdd);

            organization.Seats = (short?)valid.Value.PasswordManagerSubscriptionUpdate.Seats;

            await organizationRepository.ReplaceAsync(organization);
            await applicationCacheService.UpsertOrganizationAbilityAsync(organization);
        }
    }

    private async Task RevertSecretsManagerChangesAsync(Valid<InviteUserOrganizationValidationRequest> valid, Organization organization)
    {
        if (valid.Value.SecretsManagerSubscriptionUpdate.SeatsRequiredToAdd < 0)
        {
            var updateRevert = new SecretsManagerSubscriptionUpdate(organization, valid.Value.InviteOrganization.Plan, false)
            {
                SmSeats = valid.Value.SecretsManagerSubscriptionUpdate.Seats
            };

            await updateSecretsManagerSubscriptionCommand.UpdateSubscriptionAsync(updateRevert);
        }
    }

    private async Task PublishReferenceEventAsync(Valid<InviteUserOrganizationValidationRequest> valid,
        Organization organization) =>
        await referenceEventService.RaiseEventAsync(
            new ReferenceEvent(ReferenceEventType.InvitedUsers, organization, currentContext)
            {
                Users = valid.Value.Invites.Length
            });

    private async Task SendInvitesAsync(IEnumerable<CreateOrganizationUser> users, Organization organization) =>
        await sendOrganizationInvitesCommand.SendInvitesAsync(
            new SendInvitesRequest(
                users.Select(x => x.OrganizationUser),
                organization));

    private async Task SendAdditionalEmailsAsync(Valid<InviteUserOrganizationValidationRequest> valid, Organization organization)
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
                    .GetManyByMinimumRoleAsync(valid.Value.InviteOrganization.OrganizationId, OrganizationUserType.Owner))
                .Select(x => x.Email)
                .Distinct();

            await mailService.SendOrganizationMaxSeatLimitReachedEmailAsync(organization,
                valid.Value.PasswordManagerSubscriptionUpdate.MaxAutoScaleSeats.Value!, ownerEmails);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, IssueNotifyingOwnersOfSeatLimitReached);
        }
    }

    private async Task AdjustSecretsManagerSeatsAsync(Valid<InviteUserOrganizationValidationRequest> valid, Organization organization)
    {
        if (valid.Value.SecretsManagerSubscriptionUpdate.SeatsRequiredToAdd <= 0)
        {
            return;
        }

        var subscriptionUpdate = new SecretsManagerSubscriptionUpdate(organization, valid.Value.InviteOrganization.Plan, true)
            .AdjustSeats(valid.Value.SecretsManagerSubscriptionUpdate.SeatsRequiredToAdd);

        await updateSecretsManagerSubscriptionCommand.UpdateSubscriptionAsync(subscriptionUpdate);
    }

    private async Task AdjustPasswordManagerSeatsAsync(Valid<InviteUserOrganizationValidationRequest> valid, Organization organization)
    {
        if (valid.Value.PasswordManagerSubscriptionUpdate.SeatsRequiredToAdd <= 0)
        {
            return;
        }

        await paymentService.AdjustSeatsAsync(organization, valid.Value.InviteOrganization.Plan, valid.Value.PasswordManagerSubscriptionUpdate.SeatsRequiredToAdd);

        organization.Seats = (short?)valid.Value.PasswordManagerSubscriptionUpdate.UpdatedSeatTotal;

        await organizationRepository.ReplaceAsync(organization); // could optimize this with only a property update
        await applicationCacheService.UpsertOrganizationAbilityAsync(organization);

        await referenceEventService.RaiseEventAsync(
            new ReferenceEvent(ReferenceEventType.AdjustSeats, organization, currentContext)
            {
                PlanName = valid.Value.InviteOrganization.Plan.Name,
                PlanType = valid.Value.InviteOrganization.Plan.Type,
                Seats = valid.Value.PasswordManagerSubscriptionUpdate.UpdatedSeatTotal,
                PreviousSeats = valid.Value.PasswordManagerSubscriptionUpdate.Seats
            });
    }
}
