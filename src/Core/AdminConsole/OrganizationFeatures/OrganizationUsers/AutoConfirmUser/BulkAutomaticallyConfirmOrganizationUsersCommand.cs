using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Data.OrganizationUsers;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationConfirmation;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;
using OneOf.Types;

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
    public async Task<IEnumerable<BulkCommandResult>> RunAsync(BulkAutomaticallyConfirmOrganizationUsersRequest request)
    {
        if (request.UsersToConfirm.Count == 0)
        {
            return [];
        }

        var validationRequests = await BuildValidationRequestsAsync(request);

        // Bulk-validate; each dependency is fetched once inside the validator.
        var validationResults = (await validator.ValidateManyAsync(validationRequests, request.Organization)).ToList();

        // Collect the users that passed validation and build the to-confirm list.
        var validRequests = validationResults
            .Where(r => r.IsValid)
            .Select(r => r.Request)
            .ToList();

        if (validRequests.Count == 0)
        {
            return BuildResults(validationResults);
        }

        var confirmedRequests = await ConfirmValidatedUsersAsync(validRequests);

        if (confirmedRequests.Count == 0)
        {
            return BuildResults(validationResults);
        }

        // Run post-confirmation side effects.
        // CreateDefaultCollections must complete before triggering a sync for the user.
        await CreateDefaultCollectionsForManyAsync(confirmedRequests, request.Organization, request.DefaultUserCollectionName);

        var usersByUserId = (await userRepository.GetManyAsync(
                confirmedRequests.Select(r => r.OrganizationUser!.UserId!.Value)))
            .ToDictionary(u => u.Id);

        await Task.WhenAll(confirmedRequests.SelectMany<AutomaticallyConfirmOrganizationUserValidationRequest, Task>(r =>
        [
            LogOrganizationUserConfirmedEventAsync(r),
            SendConfirmedOrganizationUserEmailAsync(r, request.Organization, usersByUserId),
            SyncOrganizationKeysAsync(r)
        ]));

        return BuildResults(validationResults);
    }

    private static IEnumerable<BulkCommandResult> BuildResults(
        IEnumerable<ValidationResult<AutomaticallyConfirmOrganizationUserValidationRequest>> validationResults) =>
        validationResults.Select(result => result.Match(
            error => new BulkCommandResult(result.Request.OrganizationUserId, error),
            _ => new BulkCommandResult(result.Request.OrganizationUserId, new None())));

    private async Task<List<AutomaticallyConfirmOrganizationUserValidationRequest>> BuildValidationRequestsAsync(
        BulkAutomaticallyConfirmOrganizationUsersRequest request)
    {
        var orgUserIds = request.UsersToConfirm.Select(u => u.OrganizationUserId).ToList();
        var orgUsers = await organizationUserRepository.GetManyAsync(orgUserIds);
        var orgUserById = orgUsers.ToDictionary(ou => ou.Id);

        // OrganizationUser is null for not-found entries; the validator handles that as a structural error.
        return request.UsersToConfirm
            .Select(u => new AutomaticallyConfirmOrganizationUserValidationRequest
            {
                Key = u.Key,
                DefaultUserCollectionName = request.DefaultUserCollectionName,
                OrganizationUserId = u.OrganizationUserId,
                OrganizationId = request.OrganizationId,
                OrganizationUser = orgUserById.GetValueOrDefault(u.OrganizationUserId),
                Organization = request.Organization
            })
            .ToList();
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

    private async Task LogOrganizationUserConfirmedEventAsync(
        AutomaticallyConfirmOrganizationUserValidationRequest request)
    {
        try
        {
            await eventService.LogOrganizationUserEventAsync(
                request.OrganizationUser!, EventType.OrganizationUser_AutomaticallyConfirmed,
                timeProvider.GetUtcNow().UtcDateTime);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to log OrganizationUser_AutomaticallyConfirmed event.");
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
