using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;
using OneOf.Types;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.RevokeUser.v2;

public class RevokeOrganizationUserCommand(
    IOrganizationUserRepository organizationUserRepository,
    IEventService eventService,
    IPushNotificationService pushNotificationService,
    IRevokeOrganizationUserValidator validator,
    IOrganizationRepository organizationRepository,
    ILogger<RevokeOrganizationUserCommand> logger)
    : IRevokeOrganizationUserCommand
{
    public async Task<IEnumerable<BulkCommandResult>> RevokeUsersAsync(RevokeOrganizationUsersRequest request)
    {
        var validationRequest = await CreateValidationRequestsAsync(request);

        // Validate all requests
        var results = await validator.ValidateAsync(validationRequest);

        // Get validation results for individual users
        var validUsers = results.Where(r => r.IsValid).Select(r => r.Request).ToList();

        // Must be done first
        await RevokeValidUsersAsync(validUsers);

        // fire and forget these
        await LogRevokedOrganizationUsersAsync(validUsers, request.PerformedBy);
        await SendPushNotificationsAsync(validUsers);

        // Map validation results to bulk command results
        return results.Select(r => r.Match(
            error => new BulkCommandResult(r.Request.Id, error),
            _ => new BulkCommandResult(r.Request.Id, new None())
        ));
    }

    private async Task<RevokeOrganizationUsersValidationRequest> CreateValidationRequestsAsync(RevokeOrganizationUsersRequest request)
    {
        var organizationUserToRevoke = await organizationUserRepository
            .GetManyAsync(request.OrganizationUserIdsToRevoke);

        var organization = await organizationRepository.GetByIdAsync(request.OrganizationId);

        return new RevokeOrganizationUsersValidationRequest
        {
            OrganizationId = request.OrganizationId,
            OrganizationUserIdsToRevoke = request.OrganizationUserIdsToRevoke,
            PerformedBy = request.PerformedBy,
            OrganizationUsersToRevoke = organizationUserToRevoke,
            Organization = organization
        };
    }

    private async Task RevokeValidUsersAsync(ICollection<OrganizationUser> validUsers)
    {
        if (validUsers.Count == 0)
        {
            return;
        }

        await organizationUserRepository.RevokeManyByIdAsync(validUsers.Select(u => u.Id));
    }

    private async Task LogRevokedOrganizationUsersAsync(
        ICollection<OrganizationUser> revokedUsers,
        IActingUser actingUser)
    {
        if (revokedUsers.Count == 0)
        {
            return;
        }

        var eventDate = DateTime.UtcNow;

        // Log events based on who performed the action
        if (actingUser.SystemUserType.HasValue)
        {
            var revokeEventsWithSystem = revokedUsers
                .Select(user => (user, EventType.OrganizationUser_Revoked, actingUser.SystemUserType.Value, (DateTime?)eventDate))
                .ToList();
            await eventService.LogOrganizationUserEventsAsync(revokeEventsWithSystem);
        }
        else
        {
            var revokeEvents = revokedUsers
                .Select(user => (user, EventType.OrganizationUser_Revoked, (DateTime?)eventDate))
                .ToList();
            await eventService.LogOrganizationUserEventsAsync(revokeEvents);
        }
    }

    private async Task SendPushNotificationsAsync(ICollection<OrganizationUser> revokedUsers)
    {
        var userIdsToNotify = revokedUsers
            .Where(user => user.UserId.HasValue)
            .Select(user => user.UserId!.Value)
            .Distinct()
            .ToList();

        foreach (var userId in userIdsToNotify)
        {
            try
            {
                await pushNotificationService.PushSyncOrgKeysAsync(userId);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to send push notification for user {UserId}.", userId);
            }
        }
    }
}
