using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Interfaces;
using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Errors;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities.Commands;
using Bit.Core.AdminConsole.Utilities.Errors;
using Bit.Core.AdminConsole.Utilities.Validation;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptions.Interface;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Business;
using Bit.Core.Tools.Services;
using Microsoft.Extensions.Logging;
using OrganizationUserInvite = Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models.OrganizationUserInvite;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers;

public class InviteOrganizationUsersCommand(IEventService eventService,
    IOrganizationUserRepository organizationUserRepository,
    IInviteUsersValidator inviteUsersValidator,
    IPaymentService paymentService,
    IOrganizationRepository organizationRepository,
    IReferenceEventService referenceEventService,
    ICurrentContext currentContext,
    IApplicationCacheService applicationCacheService,
    IMailService mailService,
    ILogger<InviteOrganizationUsersCommand> logger,
    IUpdateSecretsManagerSubscriptionCommand updateSecretsManagerSubscriptionCommand,
    ISendOrganizationInvitesCommand sendOrganizationInvitesCommand,
    IProviderOrganizationRepository providerOrganizationRepository,
    IProviderUserRepository providerUserRepository
    ) : IInviteOrganizationUsersCommand
{

    public const string IssueNotifyingOwnersOfSeatLimitReached = "Error encountered notifying organization owners of seat limit reached.";

    public async Task<CommandResult<ScimInviteOrganizationUsersResponse>> InviteScimOrganizationUserAsync(InviteOrganizationUsersRequest request)
    {
        var result = await InviteOrganizationUsersAsync(request);

        switch (result)
        {
            case Failure<InviteOrganizationUsersResponse> failure:
                return new Failure<ScimInviteOrganizationUsersResponse>(
                    new Error<ScimInviteOrganizationUsersResponse>(failure.Error.Message,
                        new ScimInviteOrganizationUsersResponse
                        {
                            InvitedUser = failure.Error.ErroredValue.InvitedUsers.FirstOrDefault()
                        }));

            case Success<InviteOrganizationUsersResponse> success when success.Value.InvitedUsers.Any():
                var user = success.Value.InvitedUsers.First();

                await eventService.LogOrganizationUserEventAsync<IOrganizationUser>(
                    organizationUser: user,
                    type: EventType.OrganizationUser_Invited,
                    systemUser: EventSystemUser.SCIM,
                    date: request.PerformedAt.UtcDateTime);

                return new Success<ScimInviteOrganizationUsersResponse>(new ScimInviteOrganizationUsersResponse
                {
                    InvitedUser = user
                });

            default:
                return new Failure<ScimInviteOrganizationUsersResponse>(
                    new InvalidResultTypeError<ScimInviteOrganizationUsersResponse>(
                        new ScimInviteOrganizationUsersResponse()));
        }
    }

    private async Task<CommandResult<InviteOrganizationUsersResponse>> InviteOrganizationUsersAsync(InviteOrganizationUsersRequest request)
    {
        var invitesToSend = (await FilterExistingUsersAsync(request)).ToArray();

        if (invitesToSend.Length == 0)
        {
            return new Failure<InviteOrganizationUsersResponse>(new NoUsersToInviteError(
                new InviteOrganizationUsersResponse(request.InviteOrganization.OrganizationId)));
        }

        var validationResult = await inviteUsersValidator.ValidateAsync(new InviteOrganizationUsersValidationRequest
        {
            Invites = invitesToSend.ToArray(),
            InviteOrganization = request.InviteOrganization,
            PerformedBy = request.PerformedBy,
            PerformedAt = request.PerformedAt,
            OccupiedPmSeats = await organizationUserRepository.GetOccupiedSeatCountByOrganizationIdAsync(request.InviteOrganization.OrganizationId),
            OccupiedSmSeats = await organizationUserRepository.GetOccupiedSmSeatCountByOrganizationIdAsync(request.InviteOrganization.OrganizationId)
        });

        if (validationResult is Invalid<InviteOrganizationUsersValidationRequest> invalid)
        {
            return invalid.MapToFailure(r => new InviteOrganizationUsersResponse(r));
        }

        var validatedRequest = validationResult as Valid<InviteOrganizationUsersValidationRequest>;

        var organizationUserToInviteEntities = invitesToSend
            .Select(x => x.MapToDataModel(request.PerformedAt, validatedRequest!.Value.InviteOrganization))
            .ToArray();

        var organization = await organizationRepository.GetByIdAsync(validatedRequest!.Value.InviteOrganization.OrganizationId);

        try
        {
            await organizationUserRepository.CreateManyAsync(organizationUserToInviteEntities);

            await AdjustPasswordManagerSeatsAsync(validatedRequest, organization);

            await AdjustSecretsManagerSeatsAsync(validatedRequest);

            await SendAdditionalEmailsAsync(validatedRequest, organization);

            await SendInvitesAsync(organizationUserToInviteEntities, organization);

            await PublishReferenceEventAsync(validatedRequest, organization);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, FailedToInviteUsersError.Code);

            await organizationUserRepository.DeleteManyAsync(organizationUserToInviteEntities.Select(x => x.OrganizationUser.Id));

            // Do this first so that SmSeats never exceed PM seats (due to current billing requirements)
            await RevertSecretsManagerChangesAsync(validatedRequest, organization, validatedRequest.Value.InviteOrganization.SmSeats);

            await RevertPasswordManagerChangesAsync(validatedRequest, organization);

            return new Failure<InviteOrganizationUsersResponse>(
                new FailedToInviteUsersError(
                    new InviteOrganizationUsersResponse(validatedRequest.Value)));
        }

        return new Success<InviteOrganizationUsersResponse>(
            new InviteOrganizationUsersResponse(
                invitedOrganizationUsers: organizationUserToInviteEntities.Select(x => x.OrganizationUser).ToArray(),
                organizationId: organization!.Id));
    }

    private async Task<IEnumerable<OrganizationUserInvite>> FilterExistingUsersAsync(InviteOrganizationUsersRequest request)
    {
        var existingEmails = new HashSet<string>(await organizationUserRepository.SelectKnownEmailsAsync(
                request.InviteOrganization.OrganizationId, request.Invites.Select(i => i.Email), false),
            StringComparer.OrdinalIgnoreCase);

        return request.Invites
            .Where(invite => !existingEmails.Contains(invite.Email))
            .ToArray();
    }

    private async Task RevertPasswordManagerChangesAsync(Valid<InviteOrganizationUsersValidationRequest> validatedResult, Organization organization)
    {
        if (validatedResult.Value.PasswordManagerSubscriptionUpdate is { Seats: > 0, SeatsRequiredToAdd: > 0 })
        {


            await paymentService.AdjustSeatsAsync(organization,
                validatedResult.Value.InviteOrganization.Plan,
                validatedResult.Value.PasswordManagerSubscriptionUpdate.Seats.Value);

            organization.Seats = (short?)validatedResult.Value.PasswordManagerSubscriptionUpdate.Seats;

            await organizationRepository.ReplaceAsync(organization);
            await applicationCacheService.UpsertOrganizationAbilityAsync(organization);
        }
    }

    private async Task RevertSecretsManagerChangesAsync(Valid<InviteOrganizationUsersValidationRequest> validatedResult, Organization organization, int? initialSmSeats)
    {
        if (validatedResult.Value.SecretsManagerSubscriptionUpdate?.SmSeatsChanged is true)
        {
            var smSubscriptionUpdateRevert = new SecretsManagerSubscriptionUpdate(
                organization: organization,
                plan: validatedResult.Value.InviteOrganization.Plan,
                autoscaling: false)
            {
                SmSeats = initialSmSeats
            };

            await updateSecretsManagerSubscriptionCommand.UpdateSubscriptionAsync(smSubscriptionUpdateRevert);
        }
    }

    private async Task PublishReferenceEventAsync(Valid<InviteOrganizationUsersValidationRequest> validatedResult,
        Organization organization) =>
        await referenceEventService.RaiseEventAsync(
            new ReferenceEvent(ReferenceEventType.InvitedUsers, organization, currentContext)
            {
                Users = validatedResult.Value.Invites.Length
            });

    private async Task SendInvitesAsync(IEnumerable<CreateOrganizationUser> users, Organization organization) =>
        await sendOrganizationInvitesCommand.SendInvitesAsync(
            new SendInvitesRequest(
                users.Select(x => x.OrganizationUser),
                organization));

    private async Task SendAdditionalEmailsAsync(Valid<InviteOrganizationUsersValidationRequest> validatedResult, Organization organization)
    {
        await NotifyOwnersIfAutoscaleOccursAsync(validatedResult, organization);
        await NotifyOwnersIfPasswordManagerMaxSeatLimitReachedAsync(validatedResult, organization);
    }

    private async Task NotifyOwnersIfAutoscaleOccursAsync(Valid<InviteOrganizationUsersValidationRequest> validatedResult, Organization organization)
    {
        if (validatedResult.Value.PasswordManagerSubscriptionUpdate.SeatsRequiredToAdd > 0
            && !organization.OwnersNotifiedOfAutoscaling.HasValue)
        {
            await mailService.SendOrganizationAutoscaledEmailAsync(
                organization,
                validatedResult.Value.PasswordManagerSubscriptionUpdate.Seats!.Value,
                await GetOwnerEmailAddressesAsync(validatedResult.Value.InviteOrganization));

            organization.OwnersNotifiedOfAutoscaling = validatedResult.Value.PerformedAt.UtcDateTime;
            await organizationRepository.UpsertAsync(organization);
        }
    }

    private async Task NotifyOwnersIfPasswordManagerMaxSeatLimitReachedAsync(Valid<InviteOrganizationUsersValidationRequest> validatedResult, Organization organization)
    {
        if (!validatedResult.Value.PasswordManagerSubscriptionUpdate.MaxSeatsReached)
        {
            return;
        }

        try
        {
            var ownerEmails = await GetOwnerEmailAddressesAsync(validatedResult.Value.InviteOrganization);

            await mailService.SendOrganizationMaxSeatLimitReachedEmailAsync(organization,
                validatedResult.Value.PasswordManagerSubscriptionUpdate.MaxAutoScaleSeats!.Value, ownerEmails);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, IssueNotifyingOwnersOfSeatLimitReached);
        }
    }

    private async Task<IEnumerable<string>> GetOwnerEmailAddressesAsync(InviteOrganization organization)
    {
        var providerOrganization = await providerOrganizationRepository
            .GetByOrganizationId(organization.OrganizationId);

        if (providerOrganization == null)
        {
            return (await organizationUserRepository
                    .GetManyByMinimumRoleAsync(organization.OrganizationId, OrganizationUserType.Owner))
                .Select(x => x.Email)
                .Distinct();
        }

        return (await providerUserRepository
                .GetManyDetailsByProviderAsync(providerOrganization.ProviderId, ProviderUserStatusType.Confirmed))
            .Select(u => u.Email).Distinct();
    }

    private async Task AdjustSecretsManagerSeatsAsync(Valid<InviteOrganizationUsersValidationRequest> validatedResult)
    {
        if (validatedResult.Value.SecretsManagerSubscriptionUpdate?.SmSeatsChanged is true)
        {
            await updateSecretsManagerSubscriptionCommand.UpdateSubscriptionAsync(validatedResult.Value.SecretsManagerSubscriptionUpdate);
        }

    }

    private async Task AdjustPasswordManagerSeatsAsync(Valid<InviteOrganizationUsersValidationRequest> validatedResult, Organization organization)
    {
        if (validatedResult.Value.PasswordManagerSubscriptionUpdate is { SeatsRequiredToAdd: > 0, UpdatedSeatTotal: > 0 })
        {
            await paymentService.AdjustSeatsAsync(organization,
                validatedResult.Value.InviteOrganization.Plan,
                validatedResult.Value.PasswordManagerSubscriptionUpdate.UpdatedSeatTotal.Value);

            organization.Seats = (short?)validatedResult.Value.PasswordManagerSubscriptionUpdate.UpdatedSeatTotal;

            await organizationRepository.ReplaceAsync(organization); // could optimize this with only a property update
            await applicationCacheService.UpsertOrganizationAbilityAsync(organization);

            await referenceEventService.RaiseEventAsync(
                new ReferenceEvent(ReferenceEventType.AdjustSeats, organization, currentContext)
                {
                    PlanName = validatedResult.Value.InviteOrganization.Plan.Name,
                    PlanType = validatedResult.Value.InviteOrganization.Plan.Type,
                    Seats = validatedResult.Value.PasswordManagerSubscriptionUpdate.UpdatedSeatTotal,
                    PreviousSeats = validatedResult.Value.PasswordManagerSubscriptionUpdate.Seats
                });
        }
    }
}
