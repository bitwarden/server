using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Commands;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Business;
using Bit.Core.Tools.Services;
using Bit.Core.Validators;


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
        _referenceEventService = referenceEventService;
        _pushService = pushService;
        _providerUserRepository = providerUserRepository;
    }

    public async Task<(Guid OrganizationUserId, CommandResult result)> DeleteUserAsync(Guid organizationId, Guid organizationUserId, Guid? deletingUserId)
    {
        var result = await InternalDeleteManyUsersAsync(organizationId, new[] { organizationUserId }, deletingUserId);

        return result.FirstOrDefault();
    }

    public async Task<IEnumerable<(Guid OrganizationUserId, CommandResult result)>> DeleteManyUsersAsync(Guid organizationId, IEnumerable<Guid> orgUserIds, Guid? deletingUserId)
    {
        return await InternalDeleteManyUsersAsync(organizationId, orgUserIds, deletingUserId);
    }

    private async Task<IEnumerable<(Guid OrganizationUserId, CommandResult result)>> InternalDeleteManyUsersAsync(Guid organizationId, IEnumerable<Guid> orgUserIds, Guid? deletingUserId)
    {
        var orgUsers = await _organizationUserRepository.GetManyAsync(orgUserIds);
        var users = await GetUsersAsync(orgUsers);
        var managementStatus = await _getOrganizationUsersManagementStatusQuery.GetUsersOrganizationManagementStatusAsync(organizationId, orgUserIds);

        var userDeletionRequests = new List<(Guid OrganizationUserId, CommandResult result, OrganizationUser? orgUser, User? user)>();

        foreach (var orgUserId in orgUserIds)
        {
            var orgUser = orgUsers.FirstOrDefault(ou => ou.Id == orgUserId);
            if (orgUser == null || orgUser.OrganizationId != organizationId)
            {
                userDeletionRequests.Add((orgUserId, new BadRequestFailure("Member not found."), null, null));
                continue;
            }

            var user = users.FirstOrDefault(u => u.Id == orgUser.UserId);

            if (user == null)
            {
                userDeletionRequests.Add((orgUserId, new BadRequestFailure("Member not found."), orgUser, null));
                continue;
            }

            var result = await ValidateAsync(organizationId, orgUser, user, deletingUserId, managementStatus);
            if (result is not Success)
            {
                userDeletionRequests.Add((orgUserId, result, orgUser, user));
                continue;
            }

            await CancelPremiumAsync(user);

            userDeletionRequests.Add((orgUserId, new Success(), orgUser, user));
        }

        await HandleUserDeletionsAsync(userDeletionRequests);

        await LogDeletedOrganizationUsersAsync(userDeletionRequests);

        return userDeletionRequests
            .Select(request => (request.OrganizationUserId, request.result))
            .ToList();
    }

    private async Task<IEnumerable<User>> GetUsersAsync(ICollection<OrganizationUser> orgUsers)
    {
        var userIds = orgUsers
         .Where(orgUser => orgUser.UserId.HasValue)
         .Select(orgUser => orgUser.UserId!.Value)
         .ToList();

        return await _userRepository.GetManyAsync(userIds);
    }

    private async Task<CommandResult> ValidateAsync(Guid organizationId, OrganizationUser orgUser, User user, Guid? deletingUserId, IDictionary<Guid, bool> managementStatus)
    {
        var result1 = EnsureUserStatusIsNotInvited(orgUser);
        if (result1 is not Success)
        {
            return result1;
        }

        var result2 = PreventSelfDeletion(orgUser, deletingUserId);
        if (result2 is not Success)
        {
            return result2;
        }

        var validators = new[]
        {
            () => EnsureUserStatusIsNotInvited(orgUser),
            () => PreventSelfDeletion(orgUser, deletingUserId),
            () => EnsureUserIsManagedByOrganization(orgUser, managementStatus),
        };
        var result = CommandResultValidator.ExecuteValidators(validators);

        if (result is not Success)
        {
            return result;
        }

        var asyncValidators = new[]
        {
            async () => await EnsureOnlyOwnersCanDeleteOwnersAsync(organizationId, orgUser, deletingUserId),
            async () => await EnsureUserIsNotSoleOrganizationOwnerAsync(user),
            async () => await EnsureUserIsNotSoleProviderOwnerAsync(user)
        };
        var asyncResult = await CommandResultValidator.ExecuteValidatorAsync(asyncValidators);

        if (asyncResult is not Success)
        {
            return asyncResult;
        }

        return new Success();
    }
    private static CommandResult EnsureUserStatusIsNotInvited(OrganizationUser orgUser)
    {
        if (!orgUser.UserId.HasValue || orgUser.Status == OrganizationUserStatusType.Invited)
        {
            return new BadRequestFailure("You cannot delete a member with Invited status.");
        }

        return new Success();
    }
    private static CommandResult PreventSelfDeletion(OrganizationUser orgUser, Guid? deletingUserId)
    {
        if (!orgUser.UserId.HasValue || !deletingUserId.HasValue)
        {
            return new Success();
        }
        if (orgUser.UserId.Value == deletingUserId.Value)
        {
            return new BadRequestFailure("You cannot delete yourself.");
        }

        return new Success();
    }

    private async Task<CommandResult> EnsureOnlyOwnersCanDeleteOwnersAsync(Guid organizationId, OrganizationUser orgUser, Guid? deletingUserId)
    {
        if (orgUser.Type != OrganizationUserType.Owner)
        {
            return new Success();
        }

        if (deletingUserId.HasValue && !await _currentContext.OrganizationOwner(organizationId))
        {
            return new BadRequestFailure("Only owners can delete other owners.");
        }

        return new Success();
    }

    private static CommandResult EnsureUserIsManagedByOrganization(OrganizationUser orgUser, IDictionary<Guid, bool> managementStatus)
    {
        if (!managementStatus.TryGetValue(orgUser.Id, out var isManaged) || !isManaged)
        {
            return new BadRequestFailure("Member is not managed by the organization.");
        }
        return new Success();
    }

    private async Task<CommandResult> EnsureUserIsNotSoleOrganizationOwnerAsync(User user)
    {
        var onlyOwnerCount = await _organizationUserRepository.GetCountByOnlyOwnerAsync(user.Id);
        if (onlyOwnerCount > 0)
        {
            return new BadRequestFailure("Cannot delete this user because it is the sole owner of at least one organization. Please delete these organizations or upgrade another user.");
        }
        return new Success();
    }

    private async Task<CommandResult> EnsureUserIsNotSoleProviderOwnerAsync(User user)
    {
        var onlyOwnerProviderCount = await _providerUserRepository.GetCountByOnlyOwnerAsync(user.Id);
        if (onlyOwnerProviderCount > 0)
        {
            return new BadRequestFailure("Cannot delete this user because it is the sole owner of at least one provider. Please delete these providers or upgrade another user.");
        }
        return new Success();
    }

    private async Task LogDeletedOrganizationUsersAsync(
        List<(Guid OrganizationUserId, CommandResult result, OrganizationUser? orgUser, User? user)> userDeletionRequests)
    {
        var eventDate = DateTime.UtcNow;

        var events = userDeletionRequests
            .Where(request =>
                request.result is Success)
            .Select(request => (request.orgUser!, (EventType)EventType.OrganizationUser_Deleted, (DateTime?)eventDate))
            .ToList();

        if (events.Any())
        {
            await _eventService.LogOrganizationUserEventsAsync(events);
        }
    }

    private async Task HandleUserDeletionsAsync(List<(Guid OrganizationUserId, CommandResult result, OrganizationUser? orgUser, User? user)> userDeletionRequests)
    {
        var usersToDelete = userDeletionRequests
            .Where(request =>
                request.result is Success)
            .Select(request => request.user!);

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
