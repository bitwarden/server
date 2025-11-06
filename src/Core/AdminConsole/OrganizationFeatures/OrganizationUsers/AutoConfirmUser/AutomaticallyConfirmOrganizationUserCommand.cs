using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Data.OrganizationUsers;
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

        if (requestData.IsError)
        {
            return requestData.AsError;
        }

        var validatedData = await validator.ValidateAsync(requestData.AsSuccess);

        if (validatedData.IsError)
        {
            return validatedData.AsError;
        }

        var validatedRequest = validatedData.Request;

        var successfulConfirmation = await organizationUserRepository.ConfirmOrganizationUserAsync(validatedRequest.OrganizationUser);

        if (!successfulConfirmation)
        {
            return new None(); // Operation is idempotent. If this is false, then the user is already confirmed.
        }

        _ = await validatedRequest.ApplyAsync([
            CreateDefaultCollectionsAsync,
            LogOrganizationUserConfirmedEventAsync,
            SendConfirmedOrganizationUserEmailAsync,
            DeleteDeviceRegistrationAsync,
            PushSyncOrganizationKeysAsync
        ]);

        return new None(); // Operation is idempotent. If this is false, then the user is already confirmed.
    }

    private async Task<CommandResult<AutomaticallyConfirmOrganizationUserValidationRequest>> CreateDefaultCollectionsAsync(
        AutomaticallyConfirmOrganizationUserValidationRequest request)
    {
        try
        {
            if (!await ShouldCreateDefaultCollectionAsync(request))
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
            return new CommandResult<AutomaticallyConfirmOrganizationUserValidationRequest>(new FailedToCreateDefaultCollection());
        }
    }

    /// <summary>
    /// Determines whether a default collection should be created for an organization user during the confirmation process.
    /// </summary>
    /// <param name="request">
    /// The validation request containing information about the user, organization, and collection settings.
    /// </param>
    /// <returns>The result is a boolean value indicating whether a default collection should be created.</returns>
    private async Task<bool> ShouldCreateDefaultCollectionAsync(AutomaticallyConfirmOrganizationUserValidationRequest request) =>
        featureService.IsEnabled(FeatureFlagKeys.CreateDefaultLocation)
        && !string.IsNullOrWhiteSpace(request.DefaultUserCollectionName)
        && (await policyRequirementQuery.GetAsync<OrganizationDataOwnershipPolicyRequirement>(request.OrganizationUser.UserId))
            .RequiresDefaultCollectionOnConfirm(request.Organization.Id);

    private async Task<CommandResult<AutomaticallyConfirmOrganizationUserValidationRequest>> PushSyncOrganizationKeysAsync(AutomaticallyConfirmOrganizationUserValidationRequest request)
    {
        try
        {
            await pushNotificationService.PushSyncOrgKeysAsync(request.OrganizationUser.UserId);
            return request;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to push organization keys.");
            return new FailedToPushOrganizationSyncKeys();
        }
    }

    private async Task<CommandResult<AutomaticallyConfirmOrganizationUserValidationRequest>> LogOrganizationUserConfirmedEventAsync(
        AutomaticallyConfirmOrganizationUserValidationRequest request)
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

    private async Task<CommandResult<AutomaticallyConfirmOrganizationUserValidationRequest>> SendConfirmedOrganizationUserEmailAsync(
        AutomaticallyConfirmOrganizationUserValidationRequest request)
    {
        try
        {
            var user = await userRepository.GetByIdAsync(request.OrganizationUser.UserId);

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

    private async Task<CommandResult<AutomaticallyConfirmOrganizationUserValidationRequest>> DeleteDeviceRegistrationAsync(
        AutomaticallyConfirmOrganizationUserValidationRequest request)
    {
        try
        {
            var devices = (await deviceRepository.GetManyByUserIdAsync(request.OrganizationUser.UserId))
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

    private async Task<CommandResult<AutomaticallyConfirmOrganizationUserValidationRequest>> RetrieveDataAsync(
        AutomaticallyConfirmOrganizationUserRequest request)
    {
        var organizationUser = await GetOrganizationUserAsync(request);

        if (organizationUser.IsError) return organizationUser.AsError;

        var organization = await GetOrganizationAsync(request.OrganizationId);

        if (organization.IsError) return organization.AsError;

        return new AutomaticallyConfirmOrganizationUserValidationRequest
        {
            OrganizationUser = organizationUser.AsSuccess,
            Organization = organization.AsSuccess,
            PerformedBy = request.PerformedBy,
            DefaultUserCollectionName = request.DefaultUserCollectionName,
            PerformedOn = request.PerformedOn
        };
    }

    private async Task<CommandResult<AcceptedOrganizationUser>> GetOrganizationUserAsync(AutomaticallyConfirmOrganizationUserRequest request)
    {
        var organizationUser = await organizationUserRepository.GetByIdAsync(request.OrganizationUserId);

        return organizationUser switch
        {
            null or { UserId: null } => new UserNotFoundError(),
            { Status: not OrganizationUserStatusType.Accepted } => new UserIsNotAccepted(),
            _ => new AcceptedOrganizationUser(organizationUser, request.Key)
        };
    }

    private async Task<CommandResult<Organization>> GetOrganizationAsync(Guid organizationId)
    {
        var organization = await organizationRepository.GetByIdAsync(organizationId);

        return organization is not null ? organization : new OrganizationNotFound();
    }
}
