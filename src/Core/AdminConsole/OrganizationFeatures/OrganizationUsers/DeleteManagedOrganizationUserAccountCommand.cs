using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Business;
using Bit.Core.Tools.Services;


#nullable enable

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;

public class DeleteManagedOrganizationUserAccountCommand : IDeleteManagedOrganizationUserAccountCommand
{
    private readonly IUserService _userService;
    private readonly IEventService _eventService;
    private readonly IGetOrganizationUsersManagementStatusQuery _getOrganizationUsersManagementStatusQuery;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICurrentContext _currentContext;
    private readonly IHasConfirmedOwnersExceptQuery _hasConfirmedOwnersExceptQuery;
    private readonly IReferenceEventService _referenceEventService;
    private readonly IPushNotificationService _pushService;
    private readonly IProviderUserRepository _providerUserRepository;
    public DeleteManagedOrganizationUserAccountCommand(
        IUserService userService,
        IEventService eventService,
        IGetOrganizationUsersManagementStatusQuery getOrganizationUsersManagementStatusQuery,
        IOrganizationUserRepository organizationUserRepository,
        IUserRepository userRepository,
        ICurrentContext currentContext,
        IHasConfirmedOwnersExceptQuery hasConfirmedOwnersExceptQuery,
        IReferenceEventService referenceEventService,
        IPushNotificationService pushService,
        IProviderUserRepository providerUserRepository
        )
    {
        _userService = userService;
        _eventService = eventService;
        _getOrganizationUsersManagementStatusQuery = getOrganizationUsersManagementStatusQuery;
        _organizationUserRepository = organizationUserRepository;
        _userRepository = userRepository;
        _currentContext = currentContext;
        _hasConfirmedOwnersExceptQuery = hasConfirmedOwnersExceptQuery;
        _referenceEventService = referenceEventService;
        _pushService = pushService;
        _providerUserRepository = providerUserRepository;
    }

    public async Task DeleteUserAsync(Guid organizationId, Guid organizationUserId, Guid? deletingUserId)
    {
        var result = await InternalDeleteManyUsersAsync(organizationId, new[] { organizationUserId }, deletingUserId);

        var exception = result.Single().exception;

        if (exception != null)
        {
            throw exception;
        }
    }

    public async Task<IEnumerable<(Guid OrganizationUserId, string? ErrorMessage)>> DeleteManyUsersAsync(Guid organizationId, IEnumerable<Guid> orgUserIds, Guid? deletingUserId)
    {
        var userDeletionResults = await InternalDeleteManyUsersAsync(organizationId, orgUserIds, deletingUserId);

        return userDeletionResults
            .Select(result => (result.OrganizationUserId, result.exception?.Message))
            .ToList();
    }

    private async Task<IEnumerable<(Guid OrganizationUserId, OrganizationUser? orgUser, User? user, Exception? exception)>> InternalDeleteManyUsersAsync(Guid organizationId, IEnumerable<Guid> orgUserIds, Guid? deletingUserId)
    {
        var orgUsers = await _organizationUserRepository.GetManyAsync(orgUserIds);
        var users = await GetUsersAsync(orgUsers);
        var managementStatus = await _getOrganizationUsersManagementStatusQuery.GetUsersOrganizationManagementStatusAsync(organizationId, orgUserIds);
        var userDeletionResults = new List<(Guid OrganizationUserId, OrganizationUser? orgUser, User? user, Exception? exception)>();

        foreach (var orgUserId in orgUserIds)
        {
            OrganizationUser? orgUser = null;
            User? user = null;

            try
            {
                orgUser = orgUsers.FirstOrDefault(ou => ou.Id == orgUserId);
                if (orgUser == null || orgUser.OrganizationId != organizationId)
                {
                    throw new NotFoundException("Member not found.");
                }

                user = users.FirstOrDefault(u => u.Id == orgUser.UserId);

                if (user == null)
                {
                    throw new NotFoundException("Member not found.");
                }

                await ValidateAsync(organizationId, orgUser, user, deletingUserId, managementStatus);

                await CancelPremiumAsync(user);

                userDeletionResults.Add((orgUserId, orgUser, user, null));
            }
            catch (Exception exception)
            {
                userDeletionResults.Add((orgUserId, orgUser, user, exception));
            }
        }

        await HandleUserDeletionsAsync(userDeletionResults);

        await LogDeletedOrganizationUsersAsync(userDeletionResults);

        return userDeletionResults;
    }

    private async Task<IEnumerable<User>> GetUsersAsync(ICollection<OrganizationUser> orgUsers)
    {
        var userIds = orgUsers
         .Where(orgUser => orgUser.UserId.HasValue)
         .Select(orgUser => orgUser.UserId!.Value)
         .ToList();

        return await _userRepository.GetManyAsync(userIds);
    }

    private async Task ValidateAsync(Guid organizationId, OrganizationUser orgUser, User user, Guid? deletingUserId, IDictionary<Guid, bool> managementStatus)
    {
        EnsureUserStatusIsNotInvited(orgUser);
        PreventSelfDeletion(orgUser, deletingUserId);
        EnsureUserIsManagedByOrganization(orgUser, managementStatus);

        await EnsureOnlyOwnersCanDeleteOwnersAsync(organizationId, orgUser, deletingUserId);
        await EnsureUserIsNotSoleOrganizationOwnerAsync(user);
        await EnsureUserIsNotSoleProviderOwnerAsync(user);
    }
    private static void EnsureUserStatusIsNotInvited(OrganizationUser orgUser)
    {
        if (!orgUser.UserId.HasValue || orgUser.Status == OrganizationUserStatusType.Invited)
        {
            throw new BadRequestException("You cannot delete a member with Invited status.");
        }
    }
    private static void PreventSelfDeletion(OrganizationUser orgUser, Guid? deletingUserId)
    {
        if (!orgUser.UserId.HasValue || !deletingUserId.HasValue)
        {
            return;
        }
        if (orgUser.UserId.Value == deletingUserId.Value)
        {
            throw new BadRequestException("You cannot delete yourself.");
        }
    }

    private async Task EnsureOnlyOwnersCanDeleteOwnersAsync(Guid organizationId, OrganizationUser orgUser, Guid? deletingUserId)
    {
        if (orgUser.Type != OrganizationUserType.Owner)
        {
            return;
        }

        if (deletingUserId.HasValue && !await _currentContext.OrganizationOwner(organizationId))
        {
            throw new BadRequestException("Only owners can delete other owners.");
        }
    }

    private static void EnsureUserIsManagedByOrganization(OrganizationUser orgUser, IDictionary<Guid, bool> managementStatus)
    {
        if (!managementStatus.TryGetValue(orgUser.Id, out var isManaged) || !isManaged)
        {
            throw new BadRequestException("Member is not managed by the organization.");
        }
    }

    private async Task EnsureUserIsNotSoleOrganizationOwnerAsync(User user)
    {
        var onlyOwnerCount = await _organizationUserRepository.GetCountByOnlyOwnerAsync(user.Id);
        if (onlyOwnerCount > 0)
        {
            throw new BadRequestException("Cannot delete this user because it is the sole owner of at least one organization. Please delete these organizations or upgrade another user.");
        }
    }

    private async Task EnsureUserIsNotSoleProviderOwnerAsync(User user)
    {
        var onlyOwnerProviderCount = await _providerUserRepository.GetCountByOnlyOwnerAsync(user.Id);
        if (onlyOwnerProviderCount > 0)
        {
            throw new BadRequestException("Cannot delete this user because it is the sole owner of at least one provider. Please delete these providers or upgrade another user.");
        }
    }

    private async Task LogDeletedOrganizationUsersAsync(
        List<(Guid OrganizationUserId, OrganizationUser? orgUser, User? user, Exception? exception)> results)
    {
        var eventDate = DateTime.UtcNow;

        var events = results
            .Where(result =>
                result.exception == null
                && result.orgUser != null)
            .Select(result => (result.orgUser!, (EventType)EventType.OrganizationUser_Deleted, (DateTime?)eventDate))
            .ToList();

        if (events.Any())
        {
            await _eventService.LogOrganizationUserEventsAsync(events);
        }
    }
    private async Task HandleUserDeletionsAsync(List<(Guid OrganizationUserId, OrganizationUser? orgUser, User? user, Exception? exception)> userDeletionResults)
    {
        var usersToDelete = userDeletionResults
            .Where(result =>
                result.exception == null
                && result.user != null)
            .Select(i => i.user!);

        if (usersToDelete.Any())
        {
            await DeleteManyAsync(usersToDelete);
        }
    }

    private async Task DeleteManyAsync(IEnumerable<User> users)
    {
        await _userRepository.DeleteManyAsync(users);
        foreach (var user in users)
        {
            await _referenceEventService.RaiseEventAsync(
                new ReferenceEvent(ReferenceEventType.DeleteAccount, user, _currentContext));
            await _pushService.PushLogOutAsync(user.Id);
        }

    }

    private async Task CancelPremiumAsync(User user)
    {
        if (string.IsNullOrWhiteSpace(user.GatewaySubscriptionId))
        {
            return;
        }
        try
        {
            await _userService.CancelPremiumAsync(user);
        }
        catch (GatewayException) { }
    }
}
