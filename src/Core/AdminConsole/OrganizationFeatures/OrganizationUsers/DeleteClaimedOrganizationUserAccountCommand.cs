using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities.Commands;
using Bit.Core.AdminConsole.Utilities.Errors;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Business;
using Bit.Core.Tools.Services;
using Microsoft.Extensions.Logging;


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
    private readonly IReferenceEventService _referenceEventService;
    private readonly IPushNotificationService _pushService;
    public DeleteClaimedOrganizationUserAccountCommand(
        IUserService userService,
        IEventService eventService,
        IGetOrganizationUsersClaimedStatusQuery getOrganizationUsersClaimedStatusQuery,
        IOrganizationUserRepository organizationUserRepository,
        IUserRepository userRepository,
        ICurrentContext currentContext,
        IReferenceEventService referenceEventService,
        IPushNotificationService pushService,
        IProviderUserRepository providerUserRepository,
        ILogger<DeleteClaimedOrganizationUserAccountCommand> logger,
        IDeleteClaimedOrganizationUserAccountValidator deleteClaimedOrganizationUserAccountValidator)
    {
        _userService = userService;
        _eventService = eventService;
        _getOrganizationUsersClaimedStatusQuery = getOrganizationUsersClaimedStatusQuery;
        _organizationUserRepository = organizationUserRepository;
        _userRepository = userRepository;
        _currentContext = currentContext;
        _referenceEventService = referenceEventService;
        _pushService = pushService;
        _logger = logger;
        _deleteClaimedOrganizationUserAccountValidator = deleteClaimedOrganizationUserAccountValidator;
    }

    public async Task<CommandResult<DeleteUserResponse>> DeleteUserAsync(Guid organizationId, Guid organizationUserId, Guid deletingUserId)
    {
        var result = await DeleteManyUsersAsync(organizationId, [organizationUserId], deletingUserId);

        if (result.Successes.Any())
        {
            return new Success<DeleteUserResponse>(result.Successes.First());
        }

        return new Failure<DeleteUserResponse>(result.Failures.First());
    }

    public async Task<Partial<DeleteUserResponse>> DeleteManyUsersAsync(Guid organizationId, IEnumerable<Guid> orgUserIds, Guid deletingUserId)
    {
        var orgUsers = await _organizationUserRepository.GetManyAsync(orgUserIds);
        var users = await GetUsersAsync(orgUsers);
        var claimedStatuses = await _getOrganizationUsersClaimedStatusQuery.GetUsersOrganizationClaimedStatusAsync(organizationId, orgUserIds);

        var requests = CreateRequests(organizationId, deletingUserId, orgUserIds, orgUsers, users, claimedStatuses);
        var result = await _deleteClaimedOrganizationUserAccountValidator.ValidateAsync(requests);

        await CancelPremiumsAsync(result.Valid);
        await HandleUserDeletionsAsync(result.Valid);
        await LogDeletedOrganizationUsersAsync(result.Valid);

        var successes = result.Valid.Select(valid => new DeleteUserResponse { OrganizationUserId = valid.OrganizationUser!.Id });
        var errors = result.Invalid
            .Select(error => error.ToError(new DeleteUserResponse { OrganizationUserId = error.ErroredValue.OrganizationUserId }));

        return new Partial<DeleteUserResponse>(successes, errors);
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

    private async Task LogDeletedOrganizationUsersAsync(IEnumerable<DeleteUserValidationRequest> requests)
    {
        var eventDate = DateTime.UtcNow;

        var events = requests
            .Select(request => (request.OrganizationUser!, (EventType)EventType.OrganizationUser_Deleted, (DateTime?)eventDate))
            .ToList();

        if (events.Any())
        {
            await _eventService.LogOrganizationUserEventsAsync(events);
        }
    }


    private async Task HandleUserDeletionsAsync(IEnumerable<DeleteUserValidationRequest> requests)
    {
        var users = requests
            .Select(request => request.User!)
            .ToList();

        if (!users.Any())
        {
            return;
        }

        await _userRepository.DeleteManyAsync(users);

        foreach (var user in users)
        {
            await _referenceEventService.RaiseEventAsync(
                new ReferenceEvent(ReferenceEventType.DeleteAccount, user, _currentContext));
            await _pushService.PushLogOutAsync(user.Id);
        }
    }

    private async Task CancelPremiumsAsync(IEnumerable<DeleteUserValidationRequest> requests)
    {
        var users = requests
            .Select(request => request.User!);

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

