using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.Utilities.Commands;
using Bit.Core.AdminConsole.Utilities.Validation;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;


namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;
#nullable enable

public class DeleteClaimedOrganizationUserAccountCommand : IDeleteClaimedOrganizationUserAccountCommand
{
    private readonly IUserService _userService;
    private readonly IEventService _eventService;
    private readonly IGetOrganizationUsersClaimedStatusQuery _getOrganizationUsersClaimedStatusQuery;
    private readonly IDeleteClaimedOrganizationUserAccountValidator _deleteClaimedOrganizationUserAccountValidator;
    private readonly ILogger<DeleteClaimedOrganizationUserAccountCommand> _logger;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICurrentContext _currentContext;
    private readonly IHasConfirmedOwnersExceptQuery _hasConfirmedOwnersExceptQuery;
    private readonly IPushNotificationService _pushService;
    public DeleteClaimedOrganizationUserAccountCommand(
        IUserService userService,
        IEventService eventService,
        IGetOrganizationUsersClaimedStatusQuery getOrganizationUsersClaimedStatusQuery,
        IOrganizationUserRepository organizationUserRepository,
        IUserRepository userRepository,
        ICurrentContext currentContext,
        IHasConfirmedOwnersExceptQuery hasConfirmedOwnersExceptQuery,
        IPushNotificationService pushService,
        ILogger<DeleteClaimedOrganizationUserAccountCommand> logger,
        IDeleteClaimedOrganizationUserAccountValidator deleteClaimedOrganizationUserAccountValidator)
    {
        _userService = userService;
        _eventService = eventService;
        _getOrganizationUsersClaimedStatusQuery = getOrganizationUsersClaimedStatusQuery;
        _organizationUserRepository = organizationUserRepository;
        _userRepository = userRepository;
        _currentContext = currentContext;
        _hasConfirmedOwnersExceptQuery = hasConfirmedOwnersExceptQuery;
        _pushService = pushService;
        _logger = logger;
        _deleteClaimedOrganizationUserAccountValidator = deleteClaimedOrganizationUserAccountValidator;
    }

    public async Task<CommandResult<DeleteUserResponse>> DeleteUserAsync(Guid organizationId, Guid organizationUserId, Guid deletingUserId)
    {
        var result = await DeleteManyUsersAsync(organizationId, [organizationUserId], deletingUserId);
        return result.Single();
    }

    public async Task<IEnumerable<CommandResult<DeleteUserResponse>>> DeleteManyUsersAsync(Guid organizationId, IEnumerable<Guid> orgUserIds, Guid deletingUserId)
    {
        var orgUsers = await _organizationUserRepository.GetManyAsync(orgUserIds);
        var users = await GetUsersAsync(orgUsers);
        var claimedStatuses = await _getOrganizationUsersClaimedStatusQuery.GetUsersOrganizationClaimedStatusAsync(organizationId, orgUserIds);

        var requests = CreateRequests(organizationId, deletingUserId, orgUserIds, orgUsers, users, claimedStatuses);
        var validationResults = (await _deleteClaimedOrganizationUserAccountValidator.ValidateAsync(requests)).ToList();

        var validResults = validationResults.OfType<Valid<DeleteUserValidationRequest>>().ToList();
        await CancelPremiumsAsync(validResults);
        await HandleUserDeletionsAsync(validResults);
        await LogDeletedOrganizationUsersAsync(validResults);

        return validationResults.Select(v => v.ToCommandResult(new DeleteUserResponse { OrganizationUserId = v.Value.OrganizationUserId }));
    }

    private List<DeleteUserValidationRequest> CreateRequests(
        Guid organizationId,
        Guid deletingUserId,
        IEnumerable<Guid> orgUserIds,
        ICollection<OrganizationUser> orgUsers,
        IEnumerable<User> users,
        IDictionary<Guid, bool> claimedStatuses)
    {
        var requests = new List<DeleteUserValidationRequest>();
        foreach (var orgUserId in orgUserIds)
        {
            var orgUser = orgUsers.FirstOrDefault(orgUser => orgUser.Id == orgUserId);
            var user = users.FirstOrDefault(user => user.Id == orgUser?.UserId);
            claimedStatuses.TryGetValue(orgUserId, out var isClaimed);

            requests.Add(new DeleteUserValidationRequest
            {
                User = user,
                OrganizationUserId = orgUserId,
                OrganizationUser = orgUser,
                IsClaimed = isClaimed,
                OrganizationId = organizationId,
                DeletingUserId = deletingUserId,
            });
        }

        return requests;
    }

    private async Task<IEnumerable<User>> GetUsersAsync(ICollection<OrganizationUser> orgUsers)
    {
        var userIds = orgUsers
         .Where(orgUser => orgUser.UserId.HasValue)
         .Select(orgUser => orgUser.UserId!.Value)
         .ToList();

        return await _userRepository.GetManyAsync(userIds);
    }

    private async Task LogDeletedOrganizationUsersAsync(IEnumerable<Valid<DeleteUserValidationRequest>> requests)
    {
        var eventDate = DateTime.UtcNow;

        var events = requests
            .Select(request => (request.Value.OrganizationUser!, (EventType)EventType.OrganizationUser_Deleted, (DateTime?)eventDate))
            .ToList();

        if (events.Any())
        {
            await _eventService.LogOrganizationUserEventsAsync(events);
        }
    }


    private async Task HandleUserDeletionsAsync(IEnumerable<Valid<DeleteUserValidationRequest>> requests)
    {
        var users = requests
            .Select(request => request.Value.User!)
            .ToList();

        if (!users.Any())
        {
            return;
        }

        await _userRepository.DeleteManyAsync(users);

        foreach (var user in users)
        {
            await _pushService.PushLogOutAsync(user.Id);
        }
    }

    private async Task CancelPremiumsAsync(IEnumerable<Valid<DeleteUserValidationRequest>> requests)
    {
        var users = requests
            .Select(request => request.Value.User!);

        foreach (var user in users)
        {
            try
            {
                await _userService.CancelPremiumAsync(user);
            }
            catch (GatewayException exception)
            {
                _logger.LogWarning(exception, "Failed to cancel the user's premium.");
            }
        }
    }

}

