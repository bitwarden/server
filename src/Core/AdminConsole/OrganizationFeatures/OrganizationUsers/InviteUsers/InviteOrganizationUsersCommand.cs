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
            OccupiedPmSeats = (await organizationRepository.GetOccupiedSeatCountByOrganizationIdInTransactionAsync(
                request.InviteOrganization.OrganizationId,
                request.Transaction!)).Total,
            OccupiedSmSeats = await organizationUserRepository.GetOccupiedSmSeatCountByOrganizationIdInTransactionAsync(
                    request.InviteOrganization.OrganizationId,
                    request.Transaction!)
        });

        if (validationResult is Invalid<InviteOrganizationUsersValidationRequest> invalid)
        {
            return invalid.MapToFailure(r => new InviteOrganizationUsersResponse(r));
        }

        var validatedRequest = validationResult as Valid<InviteOrganizationUsersValidationRequest>;

        var organizationUserToInviteEntities = invitesToSend
            .Select(x => x.MapToDataModel(request.PerformedAt, validatedRequest!.Value.InviteOrganization))
            .ToArray();

        var organizationOptional = await organizationRepository.GetByIdInTransactionAsync(
            validatedRequest!.Value.InviteOrganization.OrganizationId,
            request.Transaction!);

        if (organizationOptional.IsT1)
            return new Failure<InviteOrganizationUsersResponse>(
                new RecordNotFoundError<InviteOrganizationUsersResponse>(new InviteOrganizationUsersResponse(validatedRequest.Value)));

        var organization = organizationOptional.AsT0;

        try
        {
            await organizationRepository.AddUsersToPasswordManagerAsync(organization!.Id,
                validatedRequest.Value.PerformedAt.UtcDateTime,
                validatedRequest.Value.PasswordManagerSubscriptionUpdate.SeatsRequiredToAdd,
                organizationUserToInviteEntities,
                request.Transaction);

            organization.Seats = validatedRequest.Value.PasswordManagerSubscriptionUpdate.UpdatedSeatTotal;
            organization.SyncSeats = true;

            await applicationCacheService.UpsertOrganizationAbilityAsync(organization);

            await AdjustSecretsManagerSeatsAsync(validatedRequest);

            await SendAdditionalEmailsAsync(validatedRequest, organization);

            await SendInvitesAsync(organizationUserToInviteEntities, organization);

            await request.Transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, FailedToInviteUsersError.Code);

            await request.Transaction.RollbackAsync();

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

    private async Task<IEnumerable<OrganizationUserInviteCommandModel>> FilterExistingUsersAsync(InviteOrganizationUsersRequest request)
    {
        var existingEmails = new HashSet<string>(await organizationUserRepository.SelectKnownEmailsInTransactionAsync(
                request.InviteOrganization.OrganizationId,
                request.Invites.Select(i => i.Email),
                false,
                request.Transaction!),
            StringComparer.OrdinalIgnoreCase);

        return request.Invites
            .Where(invite => !existingEmails.Contains(invite.Email))
            .ToArray();
    }

    private async Task RevertPasswordManagerChangesAsync(Valid<InviteOrganizationUsersValidationRequest> validatedResult, Organization organization)
    {
        if (validatedResult.Value.PasswordManagerSubscriptionUpdate is { Seats: > 0, SeatsRequiredToAdd: > 0 })
        {
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
            await organizationRepository.IncrementSeatCountAsync(
                organization.Id,
                validatedResult.Value.PasswordManagerSubscriptionUpdate.SeatsRequiredToAdd,
                validatedResult.Value.PerformedAt.UtcDateTime);

            organization.Seats = validatedResult.Value.PasswordManagerSubscriptionUpdate.UpdatedSeatTotal;
            organization.SyncSeats = true;

            await applicationCacheService.UpsertOrganizationAbilityAsync(organization);
        }
    }
}
