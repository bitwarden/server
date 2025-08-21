using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;
using OneOf;
using OneOf.Types;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.DeleteClaimedAccount;

public record CommandResult(Guid Id, OneOf<Error, None> Result);

// Not used, but for demonstration purposes, if this did return a value:
public record CommandResult<T>(Guid Id, OneOf<Error, T> Result);

public class DeleteClaimedOrganizationUserAccountCommand : IDeleteClaimedOrganizationUserAccountCommand
{
    private readonly IUserService _userService;
    private readonly IEventService _eventService;
    private readonly IGetOrganizationUsersClaimedStatusQuery _getOrganizationUsersClaimedStatusQuery;
    private readonly IDeleteClaimedOrganizationUserAccountValidator _deleteClaimedOrganizationUserAccountValidator;
    private readonly ILogger<DeleteClaimedOrganizationUserAccountCommand> _logger;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IUserRepository _userRepository;
    private readonly IPushNotificationService _pushService;
    public DeleteClaimedOrganizationUserAccountCommand(
        IUserService userService,
        IEventService eventService,
        IGetOrganizationUsersClaimedStatusQuery getOrganizationUsersClaimedStatusQuery,
        IOrganizationUserRepository organizationUserRepository,
        IUserRepository userRepository,
        IPushNotificationService pushService,
        ILogger<DeleteClaimedOrganizationUserAccountCommand> logger,
        IDeleteClaimedOrganizationUserAccountValidator deleteClaimedOrganizationUserAccountValidator)
    {
        _userService = userService;
        _eventService = eventService;
        _getOrganizationUsersClaimedStatusQuery = getOrganizationUsersClaimedStatusQuery;
        _organizationUserRepository = organizationUserRepository;
        _userRepository = userRepository;
        _pushService = pushService;
        _logger = logger;
        _deleteClaimedOrganizationUserAccountValidator = deleteClaimedOrganizationUserAccountValidator;
    }

    public async Task<CommandResult> DeleteUserAsync(Guid organizationId, Guid organizationUserId, Guid deletingUserId)
    {
        var result = await DeleteManyUsersAsync(organizationId, [organizationUserId], deletingUserId);
        return result.Single();
    }

    public async Task<IEnumerable<CommandResult>> DeleteManyUsersAsync(Guid organizationId, IEnumerable<Guid> orgUserIds, Guid deletingUserId)
    {
        orgUserIds = orgUserIds.ToList();
        var orgUsers = await _organizationUserRepository.GetManyAsync(orgUserIds);
        var users = await GetUsersAsync(orgUsers);
        var claimedStatuses = await _getOrganizationUsersClaimedStatusQuery.GetUsersOrganizationClaimedStatusAsync(organizationId, orgUserIds);

        var internalRequests = CreateInternalRequests(organizationId, deletingUserId, orgUserIds, orgUsers, users, claimedStatuses);
        var validationResults = (await _deleteClaimedOrganizationUserAccountValidator.ValidateAsync(internalRequests)).ToList();

        var validRequests = validationResults.ValidResults();
        await CancelPremiumsAsync(validRequests);
        await HandleUserDeletionsAsync(validRequests);
        await LogDeletedOrganizationUsersAsync(validRequests);

        return validationResults.Select(v => v.Error.Match<CommandResult>(
            error => new CommandResult(v.Request.OrganizationUserId, error),
            _ => new CommandResult(v.Request.OrganizationUserId, new None())
        ));
    }

    private static IEnumerable<DeleteUserValidationRequest> CreateInternalRequests(
        Guid organizationId,
        Guid deletingUserId,
        IEnumerable<Guid> orgUserIds,
        ICollection<OrganizationUser> orgUsers,
        IEnumerable<User> users,
        IDictionary<Guid, bool> claimedStatuses)
    {
        foreach (var orgUserId in orgUserIds)
        {
            var orgUser = orgUsers.FirstOrDefault(orgUser => orgUser.Id == orgUserId);
            var user = users.FirstOrDefault(user => user.Id == orgUser?.UserId);
            claimedStatuses.TryGetValue(orgUserId, out var isClaimed);

            yield return new DeleteUserValidationRequest
            {
                User = user,
                OrganizationUserId = orgUserId,
                OrganizationUser = orgUser,
                IsClaimed = isClaimed,
                OrganizationId = organizationId,
                DeletingUserId = deletingUserId,
            };
        }
    }

    private async Task<IEnumerable<User>> GetUsersAsync(ICollection<OrganizationUser> orgUsers)
    {
        var userIds = orgUsers
         .Where(orgUser => orgUser.UserId.HasValue)
         .Select(orgUser => orgUser.UserId!.Value)
         .ToList();

        return await _userRepository.GetManyAsync(userIds);
    }

    private async Task LogDeletedOrganizationUsersAsync(IEnumerable<DeleteUserValidationRequest> requests)
    {
        var eventDate = DateTime.UtcNow;

        var events = requests
            .Select(request => (request.OrganizationUser!, EventType.OrganizationUser_Deleted, (DateTime?)eventDate))
            .ToList();

        if (events.Count != 0)
        {
            await _eventService.LogOrganizationUserEventsAsync(events);
        }
    }

    private async Task HandleUserDeletionsAsync(IEnumerable<DeleteUserValidationRequest> requests)
    {
        var users = requests
            .Select(request => request.User!)
            .ToList();

        if (users.Count == 0)
        {
            return;
        }

        await _userRepository.DeleteManyAsync(users);

        foreach (var user in users)
        {
            await _pushService.PushLogOutAsync(user.Id);
        }
    }

    private async Task CancelPremiumsAsync(IEnumerable<DeleteUserValidationRequest> requests)
    {
        var users = requests.Select(request => request.User!);

        foreach (var user in users)
        {
            try
            {
                await _userService.CancelPremiumAsync(user);
            }
            catch (GatewayException exception)
            {
                _logger.LogWarning(exception, "Failed to cancel premium subscription for {userId}.", user.Id);
            }
        }
    }
}

