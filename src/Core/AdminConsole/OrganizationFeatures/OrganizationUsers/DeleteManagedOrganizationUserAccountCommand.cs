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
        await DeleteManyUsersAsync(organizationId, new[] { organizationUserId }, deletingUserId);
    }

    public async Task<IEnumerable<(Guid OrganizationUserId, string? ErrorMessage)>> DeleteManyUsersAsync(Guid organizationId, IEnumerable<Guid> orgUserIds, Guid? deletingUserId)
    {
        var orgUsers = await _organizationUserRepository.GetManyAsync(orgUserIds);
        var users = await GetUsersAsync(orgUsers);
        var managementStatus = await _getOrganizationUsersManagementStatusQuery.GetUsersOrganizationManagementStatusAsync(organizationId, orgUserIds);
        var hasOtherConfirmedOwners = await _hasConfirmedOwnersExceptQuery.HasConfirmedOwnersExceptAsync(organizationId, orgUserIds, includeProvider: true);

        var userDeletionResults = new List<(Guid OrganizationUserId, OrganizationUser? orgUser, User? user, string? ErrorMessage)>();

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

                await ValidateDeleteUserAsync(organizationId, orgUser, user, deletingUserId, managementStatus, hasOtherConfirmedOwners);

                await CancelPremiumAsync(user);

                userDeletionResults.Add((orgUserId, orgUser, user, string.Empty));
            }
            catch (Exception ex)
            {
                userDeletionResults.Add((orgUserId, orgUser, user, ex.Message));
            }
        }

        await HandleUserDeletionsAsync(userDeletionResults);

        await LogDeletedOrganizationUsersAsync(userDeletionResults);

        return userDeletionResults
            .Select(i => (i.OrganizationUserId, i.ErrorMessage))
            .ToList();
    }

    private async Task<IEnumerable<User>> GetUsersAsync(ICollection<OrganizationUser> orgUsers)
    {
        var userIds = orgUsers
         .Where(orgUser => orgUser.UserId.HasValue)
         .Select(orgUser => orgUser.UserId!.Value)
         .ToList();

        var users = await _userRepository.GetManyAsync(userIds);

        return users;
    }

    private async Task ValidateDeleteUserAsync(Guid organizationId, OrganizationUser orgUser, User user, Guid? deletingUserId, IDictionary<Guid, bool> managementStatus, bool hasOtherConfirmedOwners)
    {
        EnsureUserStatusIsNotInvited(orgUser);
        PreventSelfDeletion(orgUser, deletingUserId);
        EnsureUserIsManagedByOrganization(orgUser, managementStatus);
        PreventOrganizationSoleOwnerDeletion(orgUser, hasOtherConfirmedOwners);

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
        if (!(orgUser.UserId.HasValue && deletingUserId.HasValue))
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

    private static void PreventOrganizationSoleOwnerDeletion(OrganizationUser orgUser, bool hasOtherConfirmedOwners)
    {
        if (orgUser.Type != OrganizationUserType.Owner)
        {
            return;
        }

        if (!hasOtherConfirmedOwners)
        {
            throw new BadRequestException("Organization must have at least one confirmed owner.");
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
        List<(Guid OrganizationUserId, OrganizationUser? orgUser, User? user, string? ErrorMessage)> results)
    {
        var eventDate = DateTime.UtcNow;

        var events = results
            .Where(result => string.IsNullOrEmpty(result.ErrorMessage)
                && result.orgUser != null)
            .Select(result => (result.orgUser!, (EventType)EventType.OrganizationUser_Deleted, (DateTime?)eventDate))
            .ToList();

        if (events.Any())
        {
            await _eventService.LogOrganizationUserEventsAsync(events);
        }
    }
    private async Task HandleUserDeletionsAsync(List<(Guid OrganizationUserId, OrganizationUser? orgUser, User? user, string? ErrorMessage)> userDeletionResults)
    {
        var usersToDelete = userDeletionResults
            .Where(result =>
                string.IsNullOrEmpty(result.ErrorMessage)
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
