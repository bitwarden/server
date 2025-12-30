using Bit.Core.AdminConsole.Models.Data.OrganizationUsers;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
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
    IPolicyRequirementQuery policyRequirementQuery,
    ICollectionRepository collectionRepository,
    TimeProvider timeProvider,
    ILogger<AutomaticallyConfirmOrganizationUserCommand> logger) : IAutomaticallyConfirmOrganizationUserCommand
{
    public async Task<CommandResult> AutomaticallyConfirmOrganizationUserAsync(AutomaticallyConfirmOrganizationUserRequest request)
    {
        var validatorRequest = await RetrieveDataAsync(request);

        var validatedData = await validator.ValidateAsync(validatorRequest);

        return await validatedData.Match<Task<CommandResult>>(
            error => Task.FromResult(new CommandResult(error)),
            async _ =>
            {
                var userToConfirm = new AcceptedOrganizationUserToConfirm
                {
                    OrganizationUserId = validatedData.Request.OrganizationUser!.Id,
                    UserId = validatedData.Request.OrganizationUser.UserId!.Value,
                    Key = validatedData.Request.Key
                };

                // This operation is idempotent. If false, the user is already confirmed and no additional side effects are required.
                if (!await organizationUserRepository.ConfirmOrganizationUserAsync(userToConfirm))
                {
                    return new None();
                }

                await CreateDefaultCollectionsAsync(validatedData.Request);

                await Task.WhenAll(
                    LogOrganizationUserConfirmedEventAsync(validatedData.Request),
                    SendConfirmedOrganizationUserEmailAsync(validatedData.Request),
                    SyncOrganizationKeysAsync(validatedData.Request)
                );

                return new None();
            }
        );
    }

    private async Task SyncOrganizationKeysAsync(AutomaticallyConfirmOrganizationUserValidationRequest request)
    {
        await DeleteDeviceRegistrationAsync(request);
        await PushSyncOrganizationKeysAsync(request);
    }

    private async Task CreateDefaultCollectionsAsync(AutomaticallyConfirmOrganizationUserValidationRequest request)
    {
        try
        {
            if (!await ShouldCreateDefaultCollectionAsync(request))
            {
                return;
            }

            await collectionRepository.CreateAsync(
                new Collection
                {
                    OrganizationId = request.Organization!.Id,
                    Name = request.DefaultUserCollectionName,
                    Type = CollectionType.DefaultUserCollection
                },
                groups: null,
                [new CollectionAccessSelection
                {
                    Id = request.OrganizationUser!.Id,
                    Manage = true
                }]);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create default collection for user.");
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
        !string.IsNullOrWhiteSpace(request.DefaultUserCollectionName)
        && (await policyRequirementQuery.GetAsync<OrganizationDataOwnershipPolicyRequirement>(request.OrganizationUser!.UserId!.Value))
            .RequiresDefaultCollectionOnConfirm(request.Organization!.Id);

    private async Task PushSyncOrganizationKeysAsync(AutomaticallyConfirmOrganizationUserValidationRequest request)
    {
        try
        {
            await pushNotificationService.PushSyncOrgKeysAsync(request.OrganizationUser!.UserId!.Value);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to push organization keys.");
        }
    }

    private async Task LogOrganizationUserConfirmedEventAsync(AutomaticallyConfirmOrganizationUserValidationRequest request)
    {
        try
        {
            await eventService.LogOrganizationUserEventAsync(request.OrganizationUser,
                EventType.OrganizationUser_AutomaticallyConfirmed,
                timeProvider.GetUtcNow().UtcDateTime);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to log OrganizationUser_AutomaticallyConfirmed event.");
        }
    }

    private async Task SendConfirmedOrganizationUserEmailAsync(AutomaticallyConfirmOrganizationUserValidationRequest request)
    {
        try
        {
            var user = await userRepository.GetByIdAsync(request.OrganizationUser!.UserId!.Value);

            await mailService.SendOrganizationConfirmedEmailAsync(request.Organization!.Name,
                user!.Email,
                request.OrganizationUser.AccessSecretsManager);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send OrganizationUserConfirmed.");
        }
    }

    private async Task DeleteDeviceRegistrationAsync(AutomaticallyConfirmOrganizationUserValidationRequest request)
    {
        try
        {
            var devices = (await deviceRepository.GetManyByUserIdAsync(request.OrganizationUser!.UserId!.Value))
                    .Where(d => !string.IsNullOrWhiteSpace(d.PushToken))
                    .Select(d => d.Id.ToString());

            await pushRegistrationService.DeleteUserRegistrationOrganizationAsync(devices, request.Organization!.Id.ToString());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete device registration.");
        }
    }

    private async Task<AutomaticallyConfirmOrganizationUserValidationRequest> RetrieveDataAsync(
        AutomaticallyConfirmOrganizationUserRequest request)
    {
        return new AutomaticallyConfirmOrganizationUserValidationRequest
        {
            OrganizationUserId = request.OrganizationUserId,
            OrganizationId = request.OrganizationId,
            Key = request.Key,
            DefaultUserCollectionName = request.DefaultUserCollectionName,
            PerformedBy = request.PerformedBy,
            OrganizationUser = await organizationUserRepository.GetByIdAsync(request.OrganizationUserId),
            Organization = await organizationRepository.GetByIdAsync(request.OrganizationId)
        };
    }
}
