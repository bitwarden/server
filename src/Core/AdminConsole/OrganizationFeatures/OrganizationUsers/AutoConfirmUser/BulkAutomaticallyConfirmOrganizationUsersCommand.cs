using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Data.OrganizationUsers;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationConfirmation;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;

public class BulkAutomaticallyConfirmOrganizationUsersCommand(
    IOrganizationUserRepository organizationUserRepository,
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
    public async Task RunAsync(BulkAutomaticallyConfirmOrganizationUsersRequest request)
    {
        if (request.UsersToConfirm.Count == 0)
        {
            return;
        }

        var (organization, validationRequests) = await BuildValidationRequestsAsync(request);

        // Bulk-validate; each dependency is fetched once inside the validator.
        var validationResults = (await validator.ValidateManyAsync(validationRequests, request.OrganizationId)).ToList();

        // Collect the users that passed validation and build the to-confirm list.
        var validRequests = validationResults
            .Where(r => r.IsValid)
            .Select(r => r.Request)
            .ToList();

        if (validRequests.Count == 0)
        {
            return;
        }

        var confirmedRequests = await ConfirmValidatedUsersAsync(validRequests);

        if (confirmedRequests.Count == 0)
        {
            return;
        }

        // Run post-confirmation side effects.
        await CreateDefaultCollectionsForManyAsync(confirmedRequests, organization!, request.DefaultUserCollectionName);
        await LogOrganizationUserConfirmedEventsAsync(confirmedRequests);

        var usersByUserId = (await userRepository.GetManyAsync(
                confirmedRequests.Select(r => r.OrganizationUser!.UserId!.Value)))
            .ToDictionary(u => u.Id);

        await Task.WhenAll(confirmedRequests.SelectMany<AutomaticallyConfirmOrganizationUserValidationRequest, Task>(r =>
        [
            SendConfirmedOrganizationUserEmailAsync(r, organization!, usersByUserId),
            SyncOrganizationKeysAsync(r)
        ]));
    }

    private async Task<(
        Organization? Organization,
        List<AutomaticallyConfirmOrganizationUserValidationRequest> ValidationRequests
    )> BuildValidationRequestsAsync(BulkAutomaticallyConfirmOrganizationUsersRequest request)
    {
        var organization = request.Organization;
        var orgId = organization.Id;

        var orgUserIds = request.UsersToConfirm.Select(u => u.OrganizationUserId).ToList();
        var orgUsers = await organizationUserRepository.GetManyAsync(orgUserIds);
        var orgUserById = orgUsers.ToDictionary(ou => ou.Id);

        // Build hydrated validation requests only for users that were actually found.
        var validationRequests = request.UsersToConfirm
            .Where(u => orgUserById.ContainsKey(u.OrganizationUserId))
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

        return (organization, validationRequests);
    }

    private async Task<List<AutomaticallyConfirmOrganizationUserValidationRequest>> ConfirmValidatedUsersAsync(
        List<AutomaticallyConfirmOrganizationUserValidationRequest> validRequests)
    {
        var usersToConfirm = validRequests
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

        return validRequests
            .Where(r => confirmedIds.Contains(r.OrganizationUserId))
            .ToList();
    }

    private async Task CreateDefaultCollectionsForManyAsync(
        IEnumerable<AutomaticallyConfirmOrganizationUserValidationRequest> requests,
        Organization organization,
        string? defaultUserCollectionName)
    {
        if (string.IsNullOrWhiteSpace(defaultUserCollectionName) || !organization.UseMyItems)
        {
            return;
        }

        try
        {
            var userIds = requests.Select(r => r.OrganizationUser!.UserId!.Value).ToList();

            var policiesForUsers = await policyRequirementQuery
                .GetAsync<OrganizationDataOwnershipPolicyRequirement>(userIds);

            var eligibleOrganizationUserIds = policiesForUsers
                .Select(x => x.Requirement.GetDefaultCollectionRequestOnConfirm(organization.Id))
                .Where(w => w.ShouldCreateDefaultCollection)
                .Select(s => s.OrganizationUserId)
                .ToList();

            if (eligibleOrganizationUserIds.Count == 0)
            {
                return;
            }

            await collectionRepository.CreateDefaultCollectionsAsync(
                organization.Id,
                eligibleOrganizationUserIds,
                defaultUserCollectionName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create default collections for confirmed users.");
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

    private async Task LogOrganizationUserConfirmedEventsAsync(
        IEnumerable<AutomaticallyConfirmOrganizationUserValidationRequest> requests)
    {
        try
        {
            var now = timeProvider.GetUtcNow().UtcDateTime;
            await eventService.LogOrganizationUserEventsAsync(
                requests.Select(r => (r.OrganizationUser!, EventType.OrganizationUser_AutomaticallyConfirmed, (DateTime?)now)));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to log OrganizationUser_AutomaticallyConfirmed events.");
        }
    }

    private async Task SendConfirmedOrganizationUserEmailAsync(
        AutomaticallyConfirmOrganizationUserValidationRequest request,
        Organization organization,
        Dictionary<Guid, User> usersByUserId)
    {
        try
        {
            if (!usersByUserId.TryGetValue(request.OrganizationUser!.UserId!.Value, out var user))
            {
                return;
            }
            await sendOrganizationConfirmationCommand.SendConfirmationAsync(
                organization, user.Email, request.OrganizationUser.AccessSecretsManager);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send OrganizationUserConfirmed.");
        }
    }
}
