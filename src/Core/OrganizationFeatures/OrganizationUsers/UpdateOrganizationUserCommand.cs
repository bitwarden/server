using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.OrganizationFeatures.OrganizationUsers;

public class UpdateOrganizationUserCommand : OrganizationUserCommand, IUpdateOrganizationUserCommand
{
    private readonly IEventService _eventService;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IOrganizationService _organizationService;

    public UpdateOrganizationUserCommand(
        ICurrentContext currentContext,
        IEventService eventService,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationService organizationService)
        : base(currentContext, organizationRepository)
    {
        _eventService = eventService;
        _organizationUserRepository = organizationUserRepository;
        _organizationService = organizationService;
    }

    public async Task UpdateUserAsync(OrganizationUser user, Guid? savingUserId,
        IEnumerable<CollectionAccessSelection> collections,
        IEnumerable<Guid> groups)
    {
        if (user.Id.Equals(default(Guid)))
        {
            throw new BadRequestException("Invite the user first.");
        }

        var originalUser = await _organizationUserRepository.GetByIdAsync(user.Id);
        if (user.Equals(originalUser))
        {
            throw new BadRequestException("Please make changes before saving.");
        }

        if (savingUserId.HasValue)
        {
            await ValidateOrganizationUserUpdatePermissions(user.OrganizationId, user.Type, originalUser.Type, user.GetPermissions());
        }

        await ValidateOrganizationCustomPermissionsEnabledAsync(user.OrganizationId, user.Type);

        if (user.Type != OrganizationUserType.Owner &&
            !await _organizationService.HasConfirmedOwnersExceptAsync(user.OrganizationId, new[] { user.Id }))
        {
            throw new BadRequestException("Organization must have at least one confirmed owner.");
        }

        if (user.AccessAll)
        {
            // We don't need any collections if we're flagged to have all access.
            collections = new List<CollectionAccessSelection>();
        }
        await _organizationUserRepository.ReplaceAsync(user, collections);

        if (groups != null)
        {
            await _organizationUserRepository.UpdateGroupsAsync(user.Id, groups);
        }

        await _eventService.LogOrganizationUserEventAsync(user, EventType.OrganizationUser_Updated);
    }
}
