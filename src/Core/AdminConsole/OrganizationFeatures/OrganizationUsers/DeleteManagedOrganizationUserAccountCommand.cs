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
    private readonly IOrganizationService _organizationService;
    public DeleteManagedOrganizationUserAccountCommand(
        IUserService userService,
        IEventService eventService,
        IGetOrganizationUsersManagementStatusQuery getOrganizationUsersManagementStatusQuery,
        IOrganizationUserRepository organizationUserRepository,
        IUserRepository userRepository,
        ICurrentContext currentContext,
        IOrganizationService organizationService)
    {
        _userService = userService;
        _eventService = eventService;
        _getOrganizationUsersManagementStatusQuery = getOrganizationUsersManagementStatusQuery;
        _organizationUserRepository = organizationUserRepository;
        _userRepository = userRepository;
        _currentContext = currentContext;
        _organizationService = organizationService;
    }

    public async Task DeleteUserAsync(Guid organizationId, Guid organizationUserId, Guid? deletingUserId)
    {
        var orgUser = await _organizationUserRepository.GetByIdAsync(organizationUserId);
        if (orgUser == null || orgUser.OrganizationId != organizationId)
        {
            throw new NotFoundException("Organization user not found.");
        }

        var managementStatus = await _getOrganizationUsersManagementStatusQuery.GetUsersOrganizationManagementStatusAsync(organizationId, new[] { orgUser.Id });

        await RepositoryDeleteUserAsync(organizationId, orgUser, deletingUserId, managementStatus);

        await _eventService.LogOrganizationUserEventAsync(orgUser, EventType.OrganizationUser_Deleted);
    }

    public async Task<IEnumerable<(Guid, string)>> DeleteManyUsersAsync(Guid organizationId, IEnumerable<Guid> orgUserIds, Guid? deletingUserId)
    {
        var results = new List<(Guid, string)>();
        var orgUsers = await _organizationUserRepository.GetManyAsync(orgUserIds);
        var managementStatus = await _getOrganizationUsersManagementStatusQuery.GetUsersOrganizationManagementStatusAsync(organizationId, orgUserIds);

        foreach (var orgUserId in orgUserIds)
        {
            try
            {
                var orgUser = orgUsers.FirstOrDefault(ou => ou.Id == orgUserId);
                if (orgUser == null || orgUser.OrganizationId != organizationId)
                {
                    throw new NotFoundException("Organization user not found.");
                }

                await RepositoryDeleteUserAsync(organizationId, orgUser, deletingUserId, managementStatus);
                results.Add((orgUserId, ""));
            }
            catch (Exception e)
            {
                results.Add((orgUserId, e.Message));
            }
        }

        await LogDeletedOrganizationUsersAsync(orgUsers, results);

        return results;
    }

    private async Task RepositoryDeleteUserAsync(Guid organizationId, OrganizationUser orgUser, Guid? deletingUserId, IDictionary<Guid, bool> managementStatus)
    {
        if (!orgUser.UserId.HasValue || orgUser.Status == OrganizationUserStatusType.Invited)
        {
            throw new BadRequestException("You cannot delete a user with Invited status.");
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

            if (!await _organizationService.HasConfirmedOwnersExceptAsync(organizationId, new[] { orgUser.Id }, includeProvider: true))
            {
                throw new BadRequestException("Organization must have at least one confirmed owner.");
            }
        }

        if (!managementStatus.TryGetValue(orgUser.Id, out var isManaged) || !isManaged)
        {
            throw new BadRequestException("User is not managed by the organization.");
        }

        var userToDelete = await _userRepository.GetByIdAsync(orgUser.UserId.Value);
        if (userToDelete == null)
        {
            throw new NotFoundException("User not found.");
        }

        await _userService.DeleteAsync(userToDelete);
    }

    private async Task LogDeletedOrganizationUsersAsync(
        IEnumerable<OrganizationUser> orgUsers,
        IEnumerable<(Guid, string)> results)
    {
        var eventDate = DateTime.UtcNow;
        var events = new List<(OrganizationUser, EventType, DateTime?)>();

        foreach (var r in results)
        {
            var orgUser = orgUsers.FirstOrDefault(ou => ou.Id == r.Item1);
            // If the user was not found or there was an error, we skip logging the event
            if (orgUser == null || !string.IsNullOrEmpty(r.Item2))
            {
                continue;
            }

            events.Add((orgUser, EventType.OrganizationUser_Deleted, (DateTime?)eventDate));
        }

        if (events.Any())
        {
            await _eventService.LogOrganizationUserEventsAsync(events);
        }
    }
}
