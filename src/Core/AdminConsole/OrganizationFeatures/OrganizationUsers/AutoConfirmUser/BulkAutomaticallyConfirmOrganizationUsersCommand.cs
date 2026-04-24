using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Data.OrganizationUsers;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationConfirmation;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Bit.Core.Enums;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;

public class BulkAutomaticallyConfirmOrganizationUsersCommand(
    IOrganizationUserRepository organizationUserRepository,
    IOrganizationRepository organizationRepository,
    IBulkAutomaticallyConfirmOrganizationUsersValidator validator,
    IEventService eventService,
    IUserRepository userRepository,
    IPushRegistrationService pushRegistrationService,
    IDeviceRepository deviceRepository,
    IPushNotificationService pushNotificationService,
    IPolicyRequirementQuery policyRequirementQuery,
    ICollectionRepository collectionRepository,
    ISendOrganizationConfirmationCommand sendOrganizationConfirmationCommand,
    TimeProvider timeProvider,
    ILogger<BulkAutomaticallyConfirmOrganizationUsersCommand> logger)
    : IBulkAutomaticallyConfirmOrganizationUsersCommand
{
    public async Task<IReadOnlyList<(Guid OrganizationUserId, string? Error)>> BulkAutomaticallyConfirmOrganizationUsersAsync(
        IEnumerable<AutomaticallyConfirmOrganizationUserRequest> requests)
    {
        var requestsList = requests.ToList();

        if (requestsList.Count == 0)
        {
            return [];
        }

        // All requests must be for the same organization.
        var orgId = requestsList[0].OrganizationId;

        // Fetch org and all org-users once for the entire batch.
        var organization = await organizationRepository.GetByIdAsync(orgId);
        var orgUserIds = requestsList.Select(r => r.OrganizationUserId).ToList();
        var orgUsers = await organizationUserRepository.GetManyAsync(orgUserIds);
        var orgUserById = orgUsers.ToDictionary(ou => ou.Id);

        // Build hydrated validation requests.
        var validationRequests = requestsList
            .Select(r => new AutomaticallyConfirmOrganizationUserValidationRequest
            {
                OrganizationUserId = r.OrganizationUserId,
                OrganizationId = r.OrganizationId,
                Key = r.Key,
                DefaultUserCollectionName = r.DefaultUserCollectionName,
                PerformedBy = r.PerformedBy,
                OrganizationUser = orgUserById.TryGetValue(r.OrganizationUserId, out var ou) ? ou : null,
                Organization = organization
            })
            .ToList();

        // Bulk-validate; each dependency is fetched once inside the validator.
        var validationResults = (await validator.ValidateManyAsync(validationRequests)).ToList();

        // Collect the users that passed validation and build the to-confirm list.
        var validatedRequests = validationResults
            .Where(r => r.IsValid)
            .Select(r => r.Request)
            .ToList();

        if (validatedRequests.Count == 0)
        {
            return BuildResults(requestsList, validationResults, new HashSet<Guid>());
        }

        var usersToConfirm = validatedRequests
            .Select(r => new AcceptedOrganizationUserToConfirm
            {
                OrganizationUserId = r.OrganizationUser!.Id,
                UserId = r.OrganizationUser.UserId!.Value,
                Key = r.Key
            })
            .ToList();

        // Single SP call — confirms all eligible users atomically and returns only
        // the IDs that were actually transitioned from Accepted → Confirmed (idempotency).
        var confirmedIds = (await organizationUserRepository.ConfirmManyOrganizationUsersAsync(usersToConfirm))
            .ToHashSet();

        var confirmedRequests = validatedRequests
            .Where(r => confirmedIds.Contains(r.OrganizationUserId))
            .ToList();

        if (confirmedRequests.Count == 0)
        {
            return BuildResults(requestsList, validationResults, new HashSet<Guid>());
        }

        // Run post-confirmation side effects.
        await CreateDefaultCollectionsForManyAsync(confirmedRequests, organization!);

        await Task.WhenAll(confirmedRequests.Select(r =>
            Task.WhenAll(
                LogOrganizationUserConfirmedEventAsync(r),
                SendConfirmedOrganizationUserEmailAsync(r, organization!),
                SyncOrganizationKeysAsync(r)
            )));

        return BuildResults(requestsList, validationResults, confirmedIds);
    }

    private static IReadOnlyList<(Guid OrganizationUserId, string? Error)> BuildResults(
        IReadOnlyList<AutomaticallyConfirmOrganizationUserRequest> requests,
        IReadOnlyList<ValidationResult<AutomaticallyConfirmOrganizationUserValidationRequest>> validationResults,
        IReadOnlySet<Guid> confirmedIds)
    {
        var errorByOrgUserId = validationResults
            .Where(r => r.IsError)
            .ToDictionary(r => r.Request.OrganizationUserId, r => r.AsError.Message);

        return requests
            .Select(r =>
            {
                if (errorByOrgUserId.TryGetValue(r.OrganizationUserId, out var errorMessage))
                {
                    return (r.OrganizationUserId, (string?)errorMessage);
                }

                // No error means either confirmed or already-confirmed (idempotent).
                return (r.OrganizationUserId, (string?)null);
            })
            .ToList();
    }

    private async Task CreateDefaultCollectionsForManyAsync(
        IEnumerable<AutomaticallyConfirmOrganizationUserValidationRequest> requests,
        Organization organization)
    {
        foreach (var request in requests)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.DefaultUserCollectionName) || !organization.UseMyItems)
                {
                    continue;
                }

                var dataOwnershipRequirement = await policyRequirementQuery
                    .GetAsync<OrganizationDataOwnershipPolicyRequirement>(request.OrganizationUser!.UserId!.Value);

                if (!dataOwnershipRequirement
                        .GetDefaultCollectionRequestOnConfirm(organization.Id)
                        .ShouldCreateDefaultCollection)
                {
                    continue;
                }

                await collectionRepository.CreateDefaultCollectionsAsync(
                    organization.Id,
                    [request.OrganizationUser!.Id],
                    request.DefaultUserCollectionName);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to create default collection for user.");
            }
        }
    }

    private async Task SyncOrganizationKeysAsync(AutomaticallyConfirmOrganizationUserValidationRequest request)
    {
        await DeleteDeviceRegistrationAsync(request);
        await PushSyncOrganizationKeysAsync(request);
    }

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

    private async Task DeleteDeviceRegistrationAsync(AutomaticallyConfirmOrganizationUserValidationRequest request)
    {
        try
        {
            var devices = (await deviceRepository.GetManyByUserIdAsync(request.OrganizationUser!.UserId!.Value))
                .Where(d => !string.IsNullOrWhiteSpace(d.PushToken))
                .Select(d => d.Id.ToString());

            await pushRegistrationService.DeleteUserRegistrationOrganizationAsync(
                devices, request.Organization!.Id.ToString());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete device registration.");
        }
    }

    private async Task LogOrganizationUserConfirmedEventAsync(AutomaticallyConfirmOrganizationUserValidationRequest request)
    {
        try
        {
            await eventService.LogOrganizationUserEventAsync(
                request.OrganizationUser,
                EventType.OrganizationUser_AutomaticallyConfirmed,
                timeProvider.GetUtcNow().UtcDateTime);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to log OrganizationUser_AutomaticallyConfirmed event.");
        }
    }

    private async Task SendConfirmedOrganizationUserEmailAsync(
        AutomaticallyConfirmOrganizationUserValidationRequest request,
        Organization organization)
    {
        try
        {
            var user = await userRepository.GetByIdAsync(request.OrganizationUser!.UserId!.Value);
            await sendOrganizationConfirmationCommand.SendConfirmationAsync(
                organization, user!.Email, request.OrganizationUser.AccessSecretsManager);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send OrganizationUserConfirmed.");
        }
    }
}
