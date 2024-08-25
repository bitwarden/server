using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;

public class DeleteOrganizationUserCommand : IDeleteOrganizationUserCommand
{
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IOrganizationService _organizationService;
    private readonly IUserService _userService;
    private readonly IUserRepository _userRepository;
    private readonly IEventService _eventService;
    private readonly ICurrentContext _currentContext;
    private readonly IFeatureService _featureService;

    public DeleteOrganizationUserCommand(
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationService organizationService,
        IUserService userService,
        IUserRepository userRepository,
        IEventService eventService,
        ICurrentContext currentContext,
        IFeatureService featureService)
    {
        _organizationUserRepository = organizationUserRepository;
        _organizationService = organizationService;
        _userService = userService;
        _userRepository = userRepository;
        _eventService = eventService;
        _currentContext = currentContext;
        _featureService = featureService;
    }

    public async Task DeleteUserAsync(Guid organizationId, Guid organizationUserId, Guid? deletingUserId, OrganizationUserRemovalType removeType = OrganizationUserRemovalType.AdminRemoved)
    {
        if (_featureService.IsEnabled(FeatureFlagKeys.AccountDeprovisioning))
        {
            // If the user is being deleted by an admin or the user is leaving the organization then we need to check if the user is managed by the organization
            var userIsManagedByOrg = removeType is OrganizationUserRemovalType.AdminRemoved or OrganizationUserRemovalType.SelfRemoved
                                       && (await _organizationService.GetUsersOrganizationManagementStatusAsync(organizationId, [organizationUserId]))[organizationUserId];
            await DeleteUserAsync_vNext(organizationId, organizationUserId, deletingUserId, removeType, userIsManagedByOrg);
        }
        else
        {
            await ValidateDeleteUserAsync(organizationId, organizationUserId);

            await _organizationService.DeleteUserAsync(organizationId, organizationUserId, deletingUserId);
        }
    }

    public async Task DeleteUsersAsync(Guid organizationId, IEnumerable<Guid> organizationUserIds, Guid? deletingUserId,
        OrganizationUserRemovalType removalType = OrganizationUserRemovalType.AdminRemoved)
    {
        var usersOrganizationManagementStatus = await _organizationService.GetUsersOrganizationManagementStatusAsync(organizationId, organizationUserIds);
        foreach (var organizationUserId in organizationUserIds)
        {
            await DeleteUserAsync_vNext(organizationId, organizationUserId, deletingUserId, removalType, usersOrganizationManagementStatus[organizationUserId]);
        }
    }

    public async Task DeleteUserAsync(Guid organizationId, Guid organizationUserId, EventSystemUser eventSystemUser)
    {
        await ValidateDeleteUserAsync(organizationId, organizationUserId);

        await _organizationService.DeleteUserAsync(organizationId, organizationUserId, eventSystemUser);
    }

    private async Task<OrganizationUser> ValidateDeleteUserAsync(Guid organizationId, Guid organizationUserId)
    {
        var orgUser = await _organizationUserRepository.GetByIdAsync(organizationUserId);
        if (orgUser == null || orgUser.OrganizationId != organizationId)
        {
            throw new NotFoundException("User not found.");
        }

        return orgUser;
    }

    private async Task DeleteUserAsync_vNext(Guid organizationId, Guid organizationUserId, Guid? deletingUserId,
        OrganizationUserRemovalType removeType, bool userIsManagedByOrg)
    {
        var orgUser = await ValidateDeleteUserAsync(organizationId, organizationUserId);

        if (deletingUserId.HasValue && orgUser.UserId == deletingUserId.Value)
        {
            throw new BadRequestException("You cannot remove yourself.");
        }

        if (orgUser.Type == OrganizationUserType.Owner && deletingUserId.HasValue &&
            !await _currentContext.OrganizationOwner(organizationId))
        {
            throw new BadRequestException("Only owners can delete other owners.");
        }

        if (!await _organizationService.HasConfirmedOwnersExceptAsync(organizationId, new[] { organizationUserId }, includeProvider: true))
        {
            throw new BadRequestException("Organization must have at least one confirmed owner.");
        }

        switch (removeType)
        {
            case OrganizationUserRemovalType.AdminRemoved:
                await RepositoryDeleteOrganizationUserAsync(orgUser);
                break;
            case OrganizationUserRemovalType.AdminDeleted:
                if (userIsManagedByOrg)
                {
                    throw new BadRequestException("Cannot delete the User as it is not managed by the organization.");
                }

                if (orgUser.Status is not (OrganizationUserStatusType.Confirmed or OrganizationUserStatusType.Revoked))
                {
                    throw new BadRequestException("Cannot delete the User as it is not Confirmed or Revoked.");
                }

                if (orgUser.UserId.HasValue)
                {
                    var user = await _userRepository.GetByIdAsync(orgUser.UserId.Value);
                    await _userService.DeleteAsync(user);
                }
                else
                {
                    await RepositoryDeleteOrganizationUserAsync(orgUser);
                }
                break;
            case OrganizationUserRemovalType.SelfRemoved:
                if (userIsManagedByOrg)
                {
                    throw new BadRequestException("Cannot leave the organization as the User is managed by the organization.");
                }

                await RepositoryDeleteOrganizationUserAsync(orgUser);
                break;
            default:
                throw new NotSupportedException("Removal type not supported.");
        }

        await _eventService.LogOrganizationUserEventAsync(orgUser, (EventType)removeType);
    }

    private async Task RepositoryDeleteOrganizationUserAsync(OrganizationUser orgUser)
    {
        await _organizationUserRepository.DeleteAsync(orgUser);

        if (orgUser.UserId.HasValue)
        {
            await _organizationService.DeleteAndPushUserRegistrationAsync(orgUser.OrganizationId, orgUser.UserId.Value);
        }
    }
}
