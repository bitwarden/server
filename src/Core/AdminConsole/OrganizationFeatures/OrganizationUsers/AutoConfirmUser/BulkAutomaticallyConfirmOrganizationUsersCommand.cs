using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Data.OrganizationUsers;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.DeleteClaimedAccount;
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
        BulkAutomaticallyConfirmOrganizationUsersRequest request)
    {
        if (request.UsersToConfirm.Count == 0)
        {
            return [];
        }

        var orgId = request.OrganizationId;

        // Fetch org and all org-users once for the entire batch.
        var organization = await organizationRepository.GetByIdAsync(orgId);
        var orgUserIds = request.UsersToConfirm.Select(u => u.OrganizationUserId).ToList();
        var orgUsers = await organizationUserRepository.GetManyAsync(orgUserIds);
        var orgUserById = orgUsers.ToDictionary(ou => ou.Id);

        // Users not found in the repository are immediately invalid; track their IDs for the
        // final result so the caller gets a per-user error rather than a silent omission.
        var notFoundIds = request.UsersToConfirm
            .Select(u => u.OrganizationUserId)
            .Where(id => !orgUserById.ContainsKey(id))
            .ToHashSet();

        // Build hydrated validation requests only for users that were actually found.
        var validationRequests = request.UsersToConfirm
            .Where(u => !notFoundIds.Contains(u.OrganizationUserId))
            .Select(u => new AutomaticallyConfirmOrganizationUserValidationRequest
            {
                Key = u.Key,
                DefaultUserCollectionName = request.DefaultUserCollectionName,
                OrganizationUserId = u.OrganizationUserId,
                OrganizationId = request.OrganizationId,
                OrganizationUser = orgUserById[u.OrganizationUserId],
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
            return BuildResults(request.UsersToConfirm, validationResults, new HashSet<Guid>(), notFoundIds);
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
            return BuildResults(request.UsersToConfirm, validationResults, new HashSet<Guid>(), notFoundIds);
        }

        // Run post-confirmation side effects.
        await CreateDefaultCollectionsForManyAsync(confirmedRequests, organization!);

        await Task.WhenAll(confirmedRequests.Select(r =>
            Task.WhenAll(
                LogOrganizationUserConfirmedEventAsync(r),
                SendConfirmedOrganizationUserEmailAsync(r, organization!),
                SyncOrganizationKeysAsync(r)
            )));

        return BuildResults(request.UsersToConfirm, validationResults, confirmedIds, notFoundIds);
    }

    private static IReadOnlyList<(Guid OrganizationUserId, string? Error)> BuildResults(
        IReadOnlyList<BulkAutoConfirmUserEntry> usersToConfirm,
        IReadOnlyList<ValidationResult<AutomaticallyConfirmOrganizationUserValidationRequest>> validationResults,
        IReadOnlySet<Guid> confirmedIds,
        IReadOnlySet<Guid> notFoundIds)
    {
        var errorByOrgUserId = validationResults
            .Where(r => r.IsError)
            .ToDictionary(r => r.Request.OrganizationUserId, r => r.AsError.Message);

        return usersToConfirm
            .Select(u =>
            {
                if (notFoundIds.Contains(u.OrganizationUserId))
                {
                    return (u.OrganizationUserId, (string?)new UserNotFoundError().Message);
                }

                if (errorByOrgUserId.TryGetValue(u.OrganizationUserId, out var errorMessage))
                {
                    return (u.OrganizationUserId, (string?)errorMessage);
                }

                // No error means either confirmed or already-confirmed (idempotent).
                return (u.OrganizationUserId, (string?)null);
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
