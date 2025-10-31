using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.DeleteClaimedAccount;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;
using OneOf.Types;
using CommandResult = Bit.Core.AdminConsole.Utilities.v2.Results.CommandResult;

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
    IFeatureService featureService,
    IPolicyRequirementQuery policyRequirementQuery,
    ICollectionRepository collectionRepository,
    ILogger<AutomaticallyConfirmOrganizationUserCommand> logger) : IAutomaticallyConfirmOrganizationUserCommand
{
    public async Task<CommandResult> AutomaticallyConfirmOrganizationUserAsync(AutomaticallyConfirmOrganizationUserRequest request)
    {
        var requestData = await RetrieveDataAsync(request);

        if (requestData.IsError) return requestData.AsError;

        var validatedData = await validator.ValidateAsync(requestData.AsSuccess);

        if (validatedData.IsError) return validatedData.AsError;

        var validatedRequest = validatedData.Request;

        var successfulConfirmation = await organizationUserRepository.ConfirmOrganizationUserAsync(validatedRequest.OrganizationUser);

        if (!successfulConfirmation) return new None();

        return await validatedRequest.ToCommandResultAsync()
            .MapAsync(CreateDefaultCollectionsAsync)
            .MapAsync(LogOrganizationUserConfirmedEventAsync)
            .MapAsync(SendConfirmedOrganizationUserEmailAsync)
            .MapAsync(DeleteDeviceRegistrationAsync)
            .MapAsync(PushSyncOrganizationKeysAsync)
            .ToResultAsync();
    }

    private async Task<CommandResult<AutomaticallyConfirmOrganizationUserRequestData>> CreateDefaultCollectionsAsync(
        AutomaticallyConfirmOrganizationUserRequestData request)
    {
        try
        {
            if (!featureService.IsEnabled(FeatureFlagKeys.CreateDefaultLocation)
                || string.IsNullOrWhiteSpace(request.DefaultUserCollectionName)
                || !(await policyRequirementQuery.GetAsync<OrganizationDataOwnershipPolicyRequirement>(request.UserId))
                    .RequiresDefaultCollectionOnConfirm(request.Organization.Id))
            {
                return request;
            }

            await collectionRepository.CreateAsync(
                new Collection
                {
                    OrganizationId = request.Organization.Id,
                    Name = request.DefaultUserCollectionName,
                    Type = CollectionType.DefaultUserCollection
                },
                groups: null,
                [new CollectionAccessSelection
                {
                    Id = request.OrganizationUser.Id,
                    Manage = true
                }]);

            return request;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create default collection for user.");
            return new CommandResult<AutomaticallyConfirmOrganizationUserRequestData>(new FailedToCreateDefaultCollection());
        }
    }

    private async Task<CommandResult<AutomaticallyConfirmOrganizationUserRequestData>> PushSyncOrganizationKeysAsync(AutomaticallyConfirmOrganizationUserRequestData request)
    {
        try
        {
            await pushNotificationService.PushSyncOrgKeysAsync(request.UserId);
            return request;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to push organization keys.");
            return new FailedToPushOrganizationSyncKeys();
        }
    }

    private async Task<CommandResult<AutomaticallyConfirmOrganizationUserRequestData>> LogOrganizationUserConfirmedEventAsync(
        AutomaticallyConfirmOrganizationUserRequestData request)
    {
        try
        {
            await eventService.LogOrganizationUserEventAsync(request.OrganizationUser,
                EventType.OrganizationUser_AutomaticallyConfirmed,
                request.PerformedOn.UtcDateTime);
            return request;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to log OrganizationUser_AutomaticallyConfirmed event.");
            return new FailedToWriteToEventLog();
        }
    }

    private async Task<CommandResult<AutomaticallyConfirmOrganizationUserRequestData>> SendConfirmedOrganizationUserEmailAsync(
        AutomaticallyConfirmOrganizationUserRequestData request)
    {
        try
        {
            var user = await userRepository.GetByIdAsync(request.UserId);

            if (user is null) return new UserNotFoundError();

            await mailService.SendOrganizationConfirmedEmailAsync(request.Organization.Name,
                user.Email,
                request.OrganizationUser.AccessSecretsManager);

            return request;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send OrganizationUserConfirmed.");
            return new FailedToSendConfirmedUserEmail();
        }
    }

    private async Task<CommandResult<AutomaticallyConfirmOrganizationUserRequestData>> DeleteDeviceRegistrationAsync(
        AutomaticallyConfirmOrganizationUserRequestData request)
    {
        try
        {
            var devices = (await deviceRepository.GetManyByUserIdAsync(request.UserId))
                    .Where(d => !string.IsNullOrWhiteSpace(d.PushToken))
                    .Select(d => d.Id.ToString());

            await pushRegistrationService.DeleteUserRegistrationOrganizationAsync(devices, request.Organization.Id.ToString());
            return request;
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

        return organizationUser is { UserId: not null } ? organizationUser : new UserNotFoundError();
    }

    private async Task<CommandResult<Organization>> GetOrganizationAsync(Guid organizationId)
    {
        var organization = await organizationRepository.GetByIdAsync(organizationId);

        return organization is not null ? organization : new OrganizationNotFound();
    }
}
