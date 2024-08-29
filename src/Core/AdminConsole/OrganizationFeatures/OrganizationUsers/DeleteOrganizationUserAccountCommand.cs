using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Services;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;

public class DeleteOrganizationUserAccountCommand : IDeleteOrganizationUserAccountCommand
{
    private readonly IUserService _userService;
    private readonly IEventService _eventService;
    private readonly IGetOrganizationUsersManagementStatusQuery _getOrganizationUsersManagementStatusQuery;
    private readonly IOrganizationUserRepository _organizationUserRepository;

    public DeleteOrganizationUserAccountCommand(
        IUserService userService,
        IEventService eventService,
        IGetOrganizationUsersManagementStatusQuery getOrganizationUsersManagementStatusQuery,
        IOrganizationUserRepository organizationUserRepository)
    {
        _userService = userService;
        _eventService = eventService;
        _getOrganizationUsersManagementStatusQuery = getOrganizationUsersManagementStatusQuery;
        _organizationUserRepository = organizationUserRepository;
    }

    public async Task DeleteUserAsync(Guid organizationId, Guid organizationUserId)
    {
        var orgUser = await _organizationUserRepository.GetByIdAsync(organizationUserId);
        if (orgUser == null || orgUser.OrganizationId != organizationId)
        {
            throw new NotFoundException("Organization user not found.");
        }

        if (!orgUser.UserId.HasValue)
        {
            throw new BadRequestException("Invalid organization user.");
        }

        var managementStatus = await _getOrganizationUsersManagementStatusQuery.GetUsersOrganizationManagementStatusAsync(organizationId, new[] { orgUser.UserId.Value });
        if (!managementStatus.TryGetValue(orgUser.UserId.Value, out var isManaged) || !isManaged)
        {
            throw new BadRequestException("User is not managed by the organization.");
        }

        var userToDelete = await _userService.GetUserByIdAsync(orgUser.UserId.Value);
        if (userToDelete == null)
        {
            throw new NotFoundException("User not found.");
        }

        await _userService.DeleteAsync(userToDelete);
        await _eventService.LogOrganizationUserEventAsync(orgUser, EventType.OrganizationUser_Deleted);
    }

    public async Task<IEnumerable<(OrganizationUser, string)>> DeleteManyUsersAsync(Guid organizationId, IEnumerable<Guid> orgUserIds)
    {
        var results = new List<(OrganizationUser, string)>();
        var orgUsers = await _organizationUserRepository.GetManyAsync(orgUserIds);
        var userIds = orgUsers.Where(ou => ou.UserId.HasValue).Select(ou => ou.UserId.Value).ToList();
        var managementStatus = await _getOrganizationUsersManagementStatusQuery.GetUsersOrganizationManagementStatusAsync(organizationId, userIds);

        foreach (var orgUser in orgUsers)
        {
            if (!orgUser.UserId.HasValue)
            {
                results.Add((orgUser, "Invalid organization user."));
                continue;
            }

            if (!managementStatus.TryGetValue(orgUser.UserId.Value, out var isManaged) || !isManaged)
            {
                results.Add((orgUser, "User is not managed by the organization."));
                continue;
            }

            try
            {
                var userToDelete = await _userService.GetUserByIdAsync(orgUser.UserId.Value);
                if (userToDelete == null)
                {
                    results.Add((orgUser, "User not found."));
                    continue;
                }

                await _userService.DeleteAsync(userToDelete);
                await _eventService.LogOrganizationUserEventAsync(orgUser, EventType.OrganizationUser_Deleted);
                results.Add((orgUser, ""));
            }
            catch (Exception e)
            {
                results.Add((orgUser, e.Message));
            }
        }

        return results;
    }
}
