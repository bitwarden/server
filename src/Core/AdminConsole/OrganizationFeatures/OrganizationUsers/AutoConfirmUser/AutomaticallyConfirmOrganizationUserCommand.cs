using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.DeleteClaimedAccount;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;
using OneOf.Types;
using CommandResult = Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.DeleteClaimedAccount.CommandResult;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;

public class AutomaticallyConfirmOrganizationUserCommand(IOrganizationUserRepository organizationUserRepository,
    IOrganizationRepository organizationRepository,
    IAutomaticallyConfirmOrganizationUsersValidator validator,
    IEventService eventService,
    IMailService mailService,
    IUserRepository userRepository,
    IPushRegistrationService pushRegistrationService,
    IDeviceRepository deviceRepository,
    IPushNotificationService pushNotificationService,
    ILogger<AutomaticallyConfirmOrganizationUserCommand> logger) : IAutomaticallyConfirmOrganizationUserCommand
{
    public async Task<CommandResult> AutomaticallyConfirmOrganizationUserAsync(AutomaticallyConfirmOrganizationUserRequest request)
    {
        var requestData = await RetrieveDataAsync(request);

        if (requestData.IsError) return requestData.AsError;

        var result = await validator.ValidateAsync(requestData.AsSuccess);

        if (result.IsError) return result.AsError;

        var organizationUser = result.Request.OrganizationUser;

        var successfulConfirmation = await organizationUserRepository.ConfirmOrganizationUserAsync(organizationUser);

        if (!successfulConfirmation) return new None();

        var eventResult = await LogOrganizationUserConfirmedEventAsync(result.Request);
        if (eventResult.IsError) return eventResult.AsError;

        var emailResult = await SendConfirmedOrganizationUserEmailAsync(result.Request);
        if (emailResult.IsError) return emailResult.AsError;

        var deviceDeletionResult = await DeleteDeviceRegistrationAsync(result.Request);
        if (deviceDeletionResult.IsError) return deviceDeletionResult.AsError;

        var pushSyncKeysResult = await PushSyncOrganizationKeysAsync(result.Request);
        if (pushSyncKeysResult.IsError) return pushSyncKeysResult.AsError;

        return new None();
    }

    private async Task<CommandResult> PushSyncOrganizationKeysAsync(AutomaticallyConfirmOrganizationUserRequestData request)
    {
        try
        {
            await pushNotificationService.PushSyncOrgKeysAsync(request.UserId);
            return new None();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to push organization keys.");
            return new FailedToPushOrganizationSyncKeys();
        }
    }

    private async Task<CommandResult> LogOrganizationUserConfirmedEventAsync(
        AutomaticallyConfirmOrganizationUserRequestData request)
    {
        try
        {
            await eventService.LogOrganizationUserEventAsync(request.OrganizationUser,
                EventType.OrganizationUser_AutomaticallyConfirmed,
                request.PerformedOn.UtcDateTime);
            return new None();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to log OrganizationUser_AutomaticallyConfirmed event.");
            return new FailedToWriteToEventLog();
        }
    }

    private async Task<CommandResult> SendConfirmedOrganizationUserEmailAsync(
        AutomaticallyConfirmOrganizationUserRequestData request)
    {
        try
        {
            var user = await userRepository.GetByIdAsync(request.UserId);

            if (user is null) return new UserNotFoundError();

            await mailService.SendOrganizationConfirmedEmailAsync(request.Organization.Name,
                user.Email,
                request.OrganizationUser.AccessSecretsManager);

            return new None();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send OrganizationUserConfirmed.");
            return new FailedToSendConfirmedUserEmail();
        }
    }

    private async Task<CommandResult> DeleteDeviceRegistrationAsync(
        AutomaticallyConfirmOrganizationUserRequestData request)
    {
        try
        {
            var devices = (await deviceRepository.GetManyByUserIdAsync(request.UserId))
                    .Where(d => !string.IsNullOrWhiteSpace(d.PushToken))
                    .Select(d => d.Id.ToString());

            await pushRegistrationService.DeleteUserRegistrationOrganizationAsync(devices, request.Organization.Id.ToString());
            return new None();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete device registration.");
            return new FailedToDeleteDeviceRegistration();
        }
    }

    private async Task<CommandResult<AutomaticallyConfirmOrganizationUserRequestData>> RetrieveDataAsync(
        AutomaticallyConfirmOrganizationUserRequest request)
    {
        var organizationUser = await GetOrganizationUserAsync(request.OrganizationUserId);

        if (organizationUser.IsError) return organizationUser.AsError;
        if (organizationUser.AsSuccess.UserId is null) return new UserNotFoundError();

        var organization = await GetOrganizationAsync(request.OrganizationId);

        if (organization.IsError) return organization.AsError;

        return new AutomaticallyConfirmOrganizationUserRequestData
        {
            OrganizationUser = organizationUser.AsSuccess,
            Organization = organization.AsSuccess,
            PerformedBy = request.PerformedBy,
            Key = request.Key,
            DefaultUserCollectionName = request.DefaultUserCollectionName,
            PerformedOn = request.PerformedOn
        };
    }

    private async Task<CommandResult<OrganizationUser>> GetOrganizationUserAsync(Guid organizationUserId)
    {
        var organizationUser = await organizationUserRepository.GetByIdAsync(organizationUserId);

        return organizationUser is not null ? organizationUser : new UserNotFoundError();
    }

    private async Task<CommandResult<Organization>> GetOrganizationAsync(Guid organizationId)
    {
        var organization = await organizationRepository.GetByIdAsync(organizationId);

        return organization is not null ? organization : new OrganizationNotFound();
    }
}
