#nullable enable
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.Data;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptions.Interface;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;

public class UpdateOrganizationUserCommand : IUpdateOrganizationUserCommand
{
    private readonly IEventService _eventService;
    private readonly IOrganizationService _organizationService;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly ICountNewSmSeatsRequiredQuery _countNewSmSeatsRequiredQuery;
    private readonly IUpdateSecretsManagerSubscriptionCommand _updateSecretsManagerSubscriptionCommand;
    private readonly ICollectionRepository _collectionRepository;
    private readonly IGroupRepository _groupRepository;
    private readonly IHasConfirmedOwnersExceptQuery _hasConfirmedOwnersExceptQuery;

    public UpdateOrganizationUserCommand(
        IEventService eventService,
        IOrganizationService organizationService,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICountNewSmSeatsRequiredQuery countNewSmSeatsRequiredQuery,
        IUpdateSecretsManagerSubscriptionCommand updateSecretsManagerSubscriptionCommand,
        ICollectionRepository collectionRepository,
        IGroupRepository groupRepository,
        IHasConfirmedOwnersExceptQuery hasConfirmedOwnersExceptQuery)
    {
        _eventService = eventService;
        _organizationService = organizationService;
        _organizationRepository = organizationRepository;
        _organizationUserRepository = organizationUserRepository;
        _countNewSmSeatsRequiredQuery = countNewSmSeatsRequiredQuery;
        _updateSecretsManagerSubscriptionCommand = updateSecretsManagerSubscriptionCommand;
        _collectionRepository = collectionRepository;
        _groupRepository = groupRepository;
        _hasConfirmedOwnersExceptQuery = hasConfirmedOwnersExceptQuery;
    }

    /// <summary>
    /// Update an organization user.
    /// </summary>
    /// <param name="user">The modified user to save.</param>
    /// <param name="savingUserId">The userId of the currently logged in user who is making the change.</param>
    /// <param name="collectionAccess">The user's updated collection access. If set to null, this removes all collection access.</param>
    /// <param name="groupAccess">The user's updated group access. If set to null, groups are not updated.</param>
    /// <exception cref="BadRequestException"></exception>
    public async Task UpdateUserAsync(OrganizationUser user, Guid? savingUserId,
        List<CollectionAccessSelection>? collectionAccess, IEnumerable<Guid>? groupAccess)
    {
        // Avoid multiple enumeration
        collectionAccess = collectionAccess?.ToList();
        groupAccess = groupAccess?.ToList();

        if (user.Id.Equals(default(Guid)))
        {
            throw new BadRequestException("Invite the user first.");
        }

        var originalUser = await _organizationUserRepository.GetByIdAsync(user.Id);
        if (originalUser == null || user.OrganizationId != originalUser.OrganizationId)
        {
            throw new NotFoundException();
        }

        if (collectionAccess?.Any() == true)
        {
            await ValidateCollectionAccessAsync(originalUser, collectionAccess.ToList());
        }

        if (groupAccess?.Any() == true)
        {
            await ValidateGroupAccessAsync(originalUser, groupAccess.ToList());
        }

        if (savingUserId.HasValue)
        {
            await _organizationService.ValidateOrganizationUserUpdatePermissions(user.OrganizationId, user.Type, originalUser.Type, user.GetPermissions());
        }

        await _organizationService.ValidateOrganizationCustomPermissionsEnabledAsync(user.OrganizationId, user.Type);

        if (user.Type != OrganizationUserType.Owner &&
            !await _hasConfirmedOwnersExceptQuery.HasConfirmedOwnersExceptAsync(user.OrganizationId, new[] { user.Id }))
        {
            throw new BadRequestException("Organization must have at least one confirmed owner.");
        }

        if (collectionAccess?.Count > 0)
        {
            var invalidAssociations = collectionAccess.Where(cas => cas.Manage && (cas.ReadOnly || cas.HidePasswords));
            if (invalidAssociations.Any())
            {
                throw new BadRequestException("The Manage property is mutually exclusive and cannot be true while the ReadOnly or HidePasswords properties are also true.");
            }
        }

        // Only autoscale (if required) after all validation has passed so that we know it's a valid request before
        // updating Stripe
        if (!originalUser.AccessSecretsManager && user.AccessSecretsManager)
        {
            var additionalSmSeatsRequired = await _countNewSmSeatsRequiredQuery.CountNewSmSeatsRequiredAsync(user.OrganizationId, 1);
            if (additionalSmSeatsRequired > 0)
            {
                var organization = await _organizationRepository.GetByIdAsync(user.OrganizationId);
                var update = new SecretsManagerSubscriptionUpdate(organization, true)
                    .AdjustSeats(additionalSmSeatsRequired);
                await _updateSecretsManagerSubscriptionCommand.UpdateSubscriptionAsync(update);
            }
        }

        await _organizationUserRepository.ReplaceAsync(user, collectionAccess);

        if (groupAccess != null)
        {
            await _organizationUserRepository.UpdateGroupsAsync(user.Id, groupAccess);
        }

        await _eventService.LogOrganizationUserEventAsync(user, EventType.OrganizationUser_Updated);
    }

    private async Task ValidateCollectionAccessAsync(OrganizationUser originalUser,
        ICollection<CollectionAccessSelection> collectionAccess)
    {
        var collections = await _collectionRepository
            .GetManyByManyIdsAsync(collectionAccess.Select(c => c.Id));
        var collectionIds = collections.Select(c => c.Id);

        var missingCollection = collectionAccess
            .FirstOrDefault(cas => !collectionIds.Contains(cas.Id));
        if (missingCollection != default)
        {
            throw new NotFoundException();
        }

        var invalidCollection = collections.FirstOrDefault(c => c.OrganizationId != originalUser.OrganizationId);
        if (invalidCollection != default)
        {
            // Use generic error message to avoid enumeration
            throw new NotFoundException();
        }
    }

    private async Task ValidateGroupAccessAsync(OrganizationUser originalUser,
        ICollection<Guid> groupAccess)
    {
        var groups = await _groupRepository.GetManyByManyIds(groupAccess);
        var groupIds = groups.Select(g => g.Id);

        var missingGroupId = groupAccess.FirstOrDefault(gId => !groupIds.Contains(gId));
        if (missingGroupId != default)
        {
            throw new NotFoundException();
        }

        var invalidGroup = groups.FirstOrDefault(g => g.OrganizationId != originalUser.OrganizationId);
        if (invalidGroup != default)
        {
            // Use generic error message to avoid enumeration
            throw new NotFoundException();
        }
    }
}
