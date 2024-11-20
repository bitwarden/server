using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;

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

    public DeleteManagedOrganizationUserAccountCommand(
        IUserService userService,
        IEventService eventService,
        IGetOrganizationUsersManagementStatusQuery getOrganizationUsersManagementStatusQuery,
        IOrganizationUserRepository organizationUserRepository,
        IUserRepository userRepository,
        ICurrentContext currentContext,
        IHasConfirmedOwnersExceptQuery hasConfirmedOwnersExceptQuery)
    {
        _userService = userService;
        _eventService = eventService;
        _getOrganizationUsersManagementStatusQuery = getOrganizationUsersManagementStatusQuery;
        _organizationUserRepository = organizationUserRepository;
        _userRepository = userRepository;
        _currentContext = currentContext;
        _hasConfirmedOwnersExceptQuery = hasConfirmedOwnersExceptQuery;
    }

    public async Task DeleteUserAsync(Guid organizationId, Guid organizationUserId, Guid? deletingUserId)
    {
        var organizationUser = await _organizationUserRepository.GetByIdAsync(organizationUserId);
        if (organizationUser == null || organizationUser.OrganizationId != organizationId)
        {
            throw new NotFoundException("Member not found.");
        }

        var managementStatus = await _getOrganizationUsersManagementStatusQuery.GetUsersOrganizationManagementStatusAsync(organizationId, new[] { organizationUserId });
        var hasOtherConfirmedOwners = await _hasConfirmedOwnersExceptQuery.HasConfirmedOwnersExceptAsync(organizationId, new[] { organizationUserId }, includeProvider: true);

        await ValidateDeleteUserAsync(organizationId, organizationUser, deletingUserId, managementStatus, hasOtherConfirmedOwners);

        var user = await _userRepository.GetByIdAsync(organizationUser.UserId!.Value);
        if (user == null)
        {
            throw new NotFoundException("Member not found.");
        }

        await _userService.DeleteAsync(user);
        await _eventService.LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Deleted);
    }

    public async Task<IEnumerable<(Guid OrganizationUserId, string? ErrorMessage)>> DeleteManyUsersAsync(Guid organizationId, IEnumerable<Guid> orgUserIds, Guid? deletingUserId)
    {
        var orgUsers = await _organizationUserRepository.GetManyAsync(orgUserIds);
        var userIds = orgUsers.Where(ou => ou.UserId.HasValue).Select(ou => ou.UserId!.Value).ToList();
        var users = await _userRepository.GetManyAsync(userIds);

        var managementStatus = await _getOrganizationUsersManagementStatusQuery.GetUsersOrganizationManagementStatusAsync(organizationId, orgUserIds);
        var hasOtherConfirmedOwners = await _hasConfirmedOwnersExceptQuery.HasConfirmedOwnersExceptAsync(organizationId, orgUserIds, includeProvider: true);

        var results = new List<(Guid OrganizationUserId, string? ErrorMessage)>();
        foreach (var orgUserId in orgUserIds)
        {
            try
            {
                var orgUser = orgUsers.FirstOrDefault(ou => ou.Id == orgUserId);
                if (orgUser == null || orgUser.OrganizationId != organizationId)
                {
                    throw new NotFoundException("Member not found.");
                }

                await ValidateDeleteUserAsync(organizationId, orgUser, deletingUserId, managementStatus, hasOtherConfirmedOwners);

                var user = users.FirstOrDefault(u => u.Id == orgUser.UserId);
                if (user == null)
                {
                    throw new NotFoundException("Member not found.");
                }

                await _userService.DeleteAsync(user);
                results.Add((orgUserId, string.Empty));
            }
            catch (Exception ex)
            {
                results.Add((orgUserId, ex.Message));
            }
        }

        await LogDeletedOrganizationUsersAsync(orgUsers, results);

        return results;
    }

    private async Task ValidateDeleteUserAsync(Guid organizationId, OrganizationUser orgUser, Guid? deletingUserId, IDictionary<Guid, bool> managementStatus, bool hasOtherConfirmedOwners)
    {
        if (!orgUser.UserId.HasValue || orgUser.Status == OrganizationUserStatusType.Invited)
        {
            throw new BadRequestException("You cannot delete a member with Invited status.");
        }

        if (deletingUserId.HasValue && orgUser.UserId.Value == deletingUserId.Value)
        {
            throw new BadRequestException("You cannot delete yourself.");
        }

        if (orgUser.Type == OrganizationUserType.Owner)
        {
            if (deletingUserId.HasValue && !await _currentContext.OrganizationOwner(organizationId))
            {
                throw new BadRequestException("Only owners can delete other owners.");
            }

            if (!hasOtherConfirmedOwners)
            {
                throw new BadRequestException("Organization must have at least one confirmed owner.");
            }
        }

        if (!managementStatus.TryGetValue(orgUser.Id, out var isManaged) || !isManaged)
        {
            throw new BadRequestException("Member is not managed by the organization.");
        }
    }

    private async Task LogDeletedOrganizationUsersAsync(
        IEnumerable<OrganizationUser> orgUsers,
        IEnumerable<(Guid OrgUserId, string? ErrorMessage)> results)
    {
        var eventDate = DateTime.UtcNow;
        var events = new List<(OrganizationUser OrgUser, EventType Event, DateTime? EventDate)>();

        foreach (var (orgUserId, errorMessage) in results)
        {
            var orgUser = orgUsers.FirstOrDefault(ou => ou.Id == orgUserId);
            // If the user was not found or there was an error, we skip logging the event
            if (orgUser == null || !string.IsNullOrEmpty(errorMessage))
            {
                continue;
            }

            events.Add((orgUser, EventType.OrganizationUser_Deleted, eventDate));
        }

        if (events.Any())
        {
            await _eventService.LogOrganizationUserEventsAsync(events);
        }
    }
}
