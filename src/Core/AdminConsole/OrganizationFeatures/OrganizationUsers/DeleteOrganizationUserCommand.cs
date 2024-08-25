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

    public async Task DeleteUserAsync(Guid organizationId, Guid organizationUserId, Guid? deletingUserId, OrganizationUserRemovalType removeType = OrganizationUserRemovalType.AdminRemove)
    {
        if (_featureService.IsEnabled(FeatureFlagKeys.AccountDeprovisioning))
        {
            // We only need to check if the user is managed by the organization for admin deletions or self-removals
            var userIsManagedByOrg = removeType is OrganizationUserRemovalType.AdminDelete or OrganizationUserRemovalType.SelfRemove
                                     && (await _organizationService.GetUsersOrganizationManagementStatusAsync(organizationId, [organizationUserId]))[organizationUserId];
            await DeleteUserWithAccountDeprovisioningAsync(organizationId, organizationUserId, deletingUserId, removeType, userIsManagedByOrg);
        }
        else
        {
            await GetAndValidateDeleteUserAsync(organizationId, organizationUserId);
            await _organizationService.DeleteUserAsync(organizationId, organizationUserId, deletingUserId);
        }
    }

    public async Task DeleteUsersAsync(Guid organizationId, IEnumerable<Guid> organizationUserIds, Guid? deletingUserId,
        OrganizationUserRemovalType removalType = OrganizationUserRemovalType.AdminRemove)
    {
        if (removalType == OrganizationUserRemovalType.SelfRemove)
        {
            throw new NotSupportedException("Bulk self removal is not supported.");
        }

        var usersOrganizationManagementStatus = await _organizationService.GetUsersOrganizationManagementStatusAsync(organizationId, organizationUserIds);
        foreach (var organizationUserId in organizationUserIds)
        {
            await DeleteUserWithAccountDeprovisioningAsync(organizationId, organizationUserId, deletingUserId, removalType, usersOrganizationManagementStatus[organizationUserId]);
        }
    }

    public async Task DeleteUserAsync(Guid organizationId, Guid organizationUserId, EventSystemUser eventSystemUser)
    {
        await GetAndValidateDeleteUserAsync(organizationId, organizationUserId);
        await _organizationService.DeleteUserAsync(organizationId, organizationUserId, eventSystemUser);
    }

    private async Task<OrganizationUser> GetAndValidateDeleteUserAsync(Guid organizationId, Guid organizationUserId)
    {
        var orgUser = await _organizationUserRepository.GetByIdAsync(organizationUserId);
        if (orgUser == null || orgUser.OrganizationId != organizationId)
        {
            throw new NotFoundException("User not found.");
        }
        return orgUser;
    }

    private async Task DeleteUserWithAccountDeprovisioningAsync(Guid organizationId, Guid organizationUserId, Guid? deletingUserId,
        OrganizationUserRemovalType removeType, bool userIsManagedByOrg)
    {
        var orgUser = await GetAndValidateDeleteUserAsync(organizationId, organizationUserId);

        await ValidateUserDeletionAsync(organizationId, orgUser, deletingUserId);

        switch (removeType)
        {
            case OrganizationUserRemovalType.AdminRemove:
                await RepositoryDeleteOrganizationUserAsync(orgUser);
                break;
            case OrganizationUserRemovalType.AdminDelete:
                await HandleAdminDeleteAsync(orgUser, userIsManagedByOrg);
                break;
            case OrganizationUserRemovalType.SelfRemove:
                await HandleSelfRemoveAsync(orgUser, userIsManagedByOrg);
                break;
            default:
                throw new NotSupportedException("Removal type not supported.");
        }

        await _eventService.LogOrganizationUserEventAsync(orgUser, (EventType)removeType);
    }

    private async Task ValidateUserDeletionAsync(Guid organizationId, OrganizationUser orgUser, Guid? deletingUserId)
    {
        if (deletingUserId.HasValue && orgUser.UserId == deletingUserId.Value)
        {
            throw new BadRequestException("You cannot remove yourself.");
        }

        if (orgUser.Type == OrganizationUserType.Owner && deletingUserId.HasValue &&
            !await _currentContext.OrganizationOwner(organizationId))
        {
            throw new BadRequestException("Only owners can delete other owners.");
        }

        if (!await _organizationService.HasConfirmedOwnersExceptAsync(organizationId, [orgUser.Id], includeProvider: true))
        {
            throw new BadRequestException("Organization must have at least one confirmed owner.");
        }
    }

    private async Task HandleAdminDeleteAsync(OrganizationUser orgUser, bool userIsManagedByOrg)
    {
        if (!userIsManagedByOrg)
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
    }

    private async Task HandleSelfRemoveAsync(OrganizationUser orgUser, bool userIsManagedByOrg)
    {
        if (userIsManagedByOrg)
        {
            throw new BadRequestException("Cannot leave the organization as the User is managed by the organization.");
        }

        await RepositoryDeleteOrganizationUserAsync(orgUser);
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
