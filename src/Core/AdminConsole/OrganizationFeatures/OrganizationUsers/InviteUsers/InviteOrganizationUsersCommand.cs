// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

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
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptions.Interface;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers;

public class InviteOrganizationUsersCommand(IEventService eventService,
    IOrganizationUserRepository organizationUserRepository,
    IInviteUsersValidator inviteUsersValidator,
    IOrganizationRepository organizationRepository,
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

    public async Task<CommandResult<InviteOrganizationUsersResponse>> InviteImportedOrganizationUsersAsync(InviteOrganizationUsersRequest request)
    {
        var result = await InviteOrganizationUsersAsync(request);

        switch (result)
        {
            case Failure<InviteOrganizationUsersResponse> failure:
                return new Failure<InviteOrganizationUsersResponse>(
                        new Error<InviteOrganizationUsersResponse>(
                            failure.Error.Message,
                            new InviteOrganizationUsersResponse(failure.Error.ErroredValue.InvitedUsers, request.InviteOrganization.OrganizationId)
                            )
                        );

            case Success<InviteOrganizationUsersResponse> success when success.Value.InvitedUsers.Any():

                List<(OrganizationUser, EventType, EventSystemUser, DateTime?)> events = new List<(OrganizationUser, EventType, EventSystemUser, DateTime?)>();
                foreach (var user in success.Value.InvitedUsers)
                {
                    events.Add((user, EventType.OrganizationUser_Invited, EventSystemUser.PublicApi, request.PerformedAt.UtcDateTime));
                }

                await eventService.LogOrganizationUserEventsAsync(events);

                return new Success<InviteOrganizationUsersResponse>(new InviteOrganizationUsersResponse(success.Value.InvitedUsers, request.InviteOrganization.OrganizationId)
                );

            default:
                return new Failure<InviteOrganizationUsersResponse>(
                    new InvalidResultTypeError<InviteOrganizationUsersResponse>(
                        new InviteOrganizationUsersResponse(request.InviteOrganization.OrganizationId)));
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
            OccupiedPmSeats = (await organizationRepository.GetOccupiedSeatCountByOrganizationIdAsync(request.InviteOrganization.OrganizationId)).Total,
            OccupiedSmSeats = await organizationUserRepository.GetOccupiedSmSeatCountByOrganizationIdAsync(request.InviteOrganization.OrganizationId)
        });

        if (validationResult is Invalid<InviteOrganizationUsersValidationRequest> invalid)
        {
            return invalid.MapToFailure(r => new InviteOrganizationUsersResponse(r));
        }

        var validatedRequest = validationResult as Valid<InviteOrganizationUsersValidationRequest>;

        var organizationUserToInviteEntities = invitesToSend
            .Select(x => x.MapToDataModel(request.PerformedAt, validatedRequest!.Request.InviteOrganization))
            .ToArray();

        var organization = await organizationRepository.GetByIdAsync(validatedRequest!.Request.InviteOrganization.OrganizationId);

        try
        {
            await organizationUserRepository.CreateManyAsync(organizationUserToInviteEntities);

            await AdjustPasswordManagerSeatsAsync(validatedRequest, organization);

            await AdjustSecretsManagerSeatsAsync(validatedRequest);

            await SendAdditionalEmailsAsync(validatedRequest, organization);

            await SendInvitesAsync(organizationUserToInviteEntities, organization);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, FailedToInviteUsersError.Code);

            await organizationUserRepository.DeleteManyAsync(organizationUserToInviteEntities.Select(x => x.OrganizationUser.Id));

            // Do this first so that SmSeats never exceed PM seats (due to current billing requirements)
            await RevertSecretsManagerChangesAsync(validatedRequest, organization, validatedRequest.Request.InviteOrganization.SmSeats);

            await RevertPasswordManagerChangesAsync(validatedRequest, organization);

            return new Failure<InviteOrganizationUsersResponse>(
                new FailedToInviteUsersError(
                    new InviteOrganizationUsersResponse(validatedRequest.Request)));
        }

        return new Success<InviteOrganizationUsersResponse>(
            new InviteOrganizationUsersResponse(
                invitedOrganizationUsers: organizationUserToInviteEntities.Select(x => x.OrganizationUser).ToArray(),
                organizationId: organization!.Id));
    }

    private async Task<IEnumerable<OrganizationUserInviteCommandModel>> FilterExistingUsersAsync(InviteOrganizationUsersRequest request)
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
        if (validatedResult.Request.PasswordManagerSubscriptionUpdate is { Seats: > 0, SeatsRequiredToAdd: > 0 })
        {
            organization.Seats = (short?)validatedResult.Request.PasswordManagerSubscriptionUpdate.Seats;

            await organizationRepository.ReplaceAsync(organization);
            await applicationCacheService.UpsertOrganizationAbilityAsync(organization);
        }
    }

    private async Task RevertSecretsManagerChangesAsync(Valid<InviteOrganizationUsersValidationRequest> validatedResult, Organization organization, int? initialSmSeats)
    {
        if (validatedResult.Request.SecretsManagerSubscriptionUpdate?.SmSeatsChanged is true)
        {
            var smSubscriptionUpdateRevert = new SecretsManagerSubscriptionUpdate(
                organization: organization,
                plan: validatedResult.Request.InviteOrganization.Plan,
                autoscaling: false)
            {
                SmSeats = initialSmSeats
            };

            await updateSecretsManagerSubscriptionCommand.UpdateSubscriptionAsync(smSubscriptionUpdateRevert);
        }
    }

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
        if (validatedResult.Request.PasswordManagerSubscriptionUpdate.SeatsRequiredToAdd > 0
            && !organization.OwnersNotifiedOfAutoscaling.HasValue)
        {
            await mailService.SendOrganizationAutoscaledEmailAsync(
                organization,
                validatedResult.Request.PasswordManagerSubscriptionUpdate.Seats!.Value,
                await GetOwnerEmailAddressesAsync(validatedResult.Request.InviteOrganization));

            organization.OwnersNotifiedOfAutoscaling = validatedResult.Request.PerformedAt.UtcDateTime;
            await organizationRepository.UpsertAsync(organization);
        }
    }

    private async Task NotifyOwnersIfPasswordManagerMaxSeatLimitReachedAsync(Valid<InviteOrganizationUsersValidationRequest> validatedResult, Organization organization)
    {
        if (!validatedResult.Request.PasswordManagerSubscriptionUpdate.MaxSeatsReached)
        {
            return;
        }

        try
        {
            var ownerEmails = await GetOwnerEmailAddressesAsync(validatedResult.Request.InviteOrganization);

            await mailService.SendOrganizationMaxSeatLimitReachedEmailAsync(organization,
                validatedResult.Request.PasswordManagerSubscriptionUpdate.MaxAutoScaleSeats!.Value, ownerEmails);
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
        if (validatedResult.Request.SecretsManagerSubscriptionUpdate?.SmSeatsChanged is true)
        {
            await updateSecretsManagerSubscriptionCommand.UpdateSubscriptionAsync(validatedResult.Request.SecretsManagerSubscriptionUpdate);
        }

    }

    private async Task AdjustPasswordManagerSeatsAsync(Valid<InviteOrganizationUsersValidationRequest> validatedResult, Organization organization)
    {
        if (validatedResult.Request.PasswordManagerSubscriptionUpdate is { SeatsRequiredToAdd: > 0, UpdatedSeatTotal: > 0 })
        {
            await organizationRepository.IncrementSeatCountAsync(
                organization.Id,
                validatedResult.Request.PasswordManagerSubscriptionUpdate.SeatsRequiredToAdd,
                validatedResult.Request.PerformedAt.UtcDateTime);

            organization.Seats = validatedResult.Request.PasswordManagerSubscriptionUpdate.UpdatedSeatTotal;
            organization.SyncSeats = true;

            await applicationCacheService.UpsertOrganizationAbilityAsync(organization);
        }
    }
}
