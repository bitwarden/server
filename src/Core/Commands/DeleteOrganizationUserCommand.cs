using Bit.Core.Commands.Interfaces;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Queries.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.Commands;

public class DeleteOrganizationUserCommand : IDeleteOrganizationUserCommand
{
    private readonly ICurrentContext _currentContext;
    private readonly IEventService _eventService;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IOrganizationHasConfirmedOwnersExceptQuery _organizationHasConfirmedOwnersExceptQuery;
    private readonly IPushDeleteUserRegistrationOrganizationCommand _pushDeleteUserRegistrationOrganizationCommand;

    public DeleteOrganizationUserCommand(
        ICurrentContext currentContext,
        IEventService eventService,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationHasConfirmedOwnersExceptQuery organizationHasConfirmedOwnersExceptQuery,
        IPushDeleteUserRegistrationOrganizationCommand pushDeleteUserRegistrationOrganizationCommand)
    {
        _currentContext = currentContext;
        _eventService = eventService;
        _organizationUserRepository = organizationUserRepository;
        _organizationHasConfirmedOwnersExceptQuery = organizationHasConfirmedOwnersExceptQuery;
        _pushDeleteUserRegistrationOrganizationCommand = pushDeleteUserRegistrationOrganizationCommand;
    }

    public async Task DeleteUserAsync(Guid organizationId, Guid organizationUserId, Guid? deletingUserId)
    {
        var orgUser = await _organizationUserRepository.GetByIdAsync(organizationUserId);
        if (orgUser == null || orgUser.OrganizationId != organizationId)
        {
            throw new NotFoundException("User not found.");
        }

        if (deletingUserId.HasValue && orgUser.UserId == deletingUserId.Value)
        {
            throw new BadRequestException("You cannot remove yourself.");
        }

        if (orgUser.Type == OrganizationUserType.Owner && deletingUserId.HasValue &&
            !await _currentContext.OrganizationOwner(organizationId))
        {
            throw new BadRequestException("Only owners can delete other owners.");
        }

        if (!await _organizationHasConfirmedOwnersExceptQuery.HasConfirmedOwnersExceptAsync(organizationId, new[] { organizationUserId }))
        {
            throw new BadRequestException("Organization must have at least one confirmed owner.");
        }

        await _organizationUserRepository.DeleteAsync(orgUser);
        await _eventService.LogOrganizationUserEventAsync(orgUser, EventType.OrganizationUser_Removed);

        if (orgUser.UserId.HasValue)
        {
            await _pushDeleteUserRegistrationOrganizationCommand.DeleteAndPushUserRegistrationAsync(organizationId, orgUser.UserId.Value);
        }
    }
}
