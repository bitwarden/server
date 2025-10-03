#nullable enable
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Pricing;
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
    private readonly IPricingClient _pricingClient;

    public UpdateOrganizationUserCommand(
        IEventService eventService,
        IOrganizationService organizationService,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICountNewSmSeatsRequiredQuery countNewSmSeatsRequiredQuery,
        IUpdateSecretsManagerSubscriptionCommand updateSecretsManagerSubscriptionCommand,
        ICollectionRepository collectionRepository,
        IGroupRepository groupRepository,
        IHasConfirmedOwnersExceptQuery hasConfirmedOwnersExceptQuery,
        IPricingClient pricingClient)
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
        _pricingClient = pricingClient;
    }

    /// <summary>
    /// Update an organization user.
    /// </summary>
    /// <param name="organizationUser">The modified organization user to save.</param>
    /// <param name="existingUserType">The current type (member role) of the user.</param>
    /// <param name="savingUserId">The userId of the currently logged in user who is making the change.</param>
    /// <param name="collectionAccess">The user's updated collection access. If set to null, this removes all collection access.</param>
    /// <param name="groupAccess">The user's updated group access. If set to null, groups are not updated.</param>
    /// <exception cref="BadRequestException"></exception>
    public async Task UpdateUserAsync(OrganizationUser organizationUser, OrganizationUserType existingUserType,
        Guid? savingUserId,
        List<CollectionAccessSelection>? collectionAccess, IEnumerable<Guid>? groupAccess)
    {
        // Avoid multiple enumeration
        var collectionAccessList = collectionAccess?.ToList() ?? [];
        groupAccess = groupAccess?.ToList();

        if (organizationUser.Id.Equals(Guid.Empty))
        {
            throw new BadRequestException("Invite the user first.");
        }

        var originalOrganizationUser = await _organizationUserRepository.GetByIdAsync(organizationUser.Id);
        if (originalOrganizationUser == null || organizationUser.OrganizationId != originalOrganizationUser.OrganizationId)
        {
            throw new NotFoundException();
        }

        var organization = await _organizationRepository.GetByIdAsync(organizationUser.OrganizationId);
        if (organization == null)
        {
            throw new NotFoundException();
        }

        await EnsureUserCannotBeAdminOrOwnerForMultipleFreeOrganizationAsync(organizationUser, existingUserType, organization);

        if (collectionAccessList.Count != 0)
        {
            collectionAccessList = await ValidateAccessAndFilterDefaultUserCollectionsAsync(originalOrganizationUser, collectionAccessList);
        }

        if (groupAccess?.Any() == true)
        {
            await ValidateGroupAccessAsync(originalOrganizationUser, groupAccess.ToList());
        }

        if (savingUserId.HasValue)
        {
            await _organizationService.ValidateOrganizationUserUpdatePermissions(organizationUser.OrganizationId, organizationUser.Type, originalOrganizationUser.Type, organizationUser.GetPermissions());
        }

        await _organizationService.ValidateOrganizationCustomPermissionsEnabledAsync(organizationUser.OrganizationId, organizationUser.Type);

        if (organizationUser.Type != OrganizationUserType.Owner &&
            !await _hasConfirmedOwnersExceptQuery.HasConfirmedOwnersExceptAsync(organizationUser.OrganizationId,
                [organizationUser.Id]))
        {
            throw new BadRequestException("Organization must have at least one confirmed owner.");
        }

        if (collectionAccessList.Count > 0)
        {
            var invalidAssociations = collectionAccessList.Where(cas => cas.Manage && (cas.ReadOnly || cas.HidePasswords));
            if (invalidAssociations.Any())
            {
                throw new BadRequestException("The Manage property is mutually exclusive and cannot be true while the ReadOnly or HidePasswords properties are also true.");
            }
        }

        // Only autoscale (if required) after all validation has passed so that we know it's a valid request before
        // updating Stripe
        if (!originalOrganizationUser.AccessSecretsManager && organizationUser.AccessSecretsManager)
        {
            var additionalSmSeatsRequired = await _countNewSmSeatsRequiredQuery.CountNewSmSeatsRequiredAsync(organizationUser.OrganizationId, 1);
            if (additionalSmSeatsRequired > 0)
            {
                // TODO: https://bitwarden.atlassian.net/browse/PM-17012
                var plan = await _pricingClient.GetPlanOrThrow(organization.PlanType);
                var update = new SecretsManagerSubscriptionUpdate(organization, plan, true)
                    .AdjustSeats(additionalSmSeatsRequired);
                await _updateSecretsManagerSubscriptionCommand.UpdateSubscriptionAsync(update);
            }
        }

        await _organizationUserRepository.ReplaceAsync(organizationUser, collectionAccessList);

        if (groupAccess != null)
        {
            await _organizationUserRepository.UpdateGroupsAsync(organizationUser.Id, groupAccess);
        }

        await _eventService.LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Updated);
    }

    private async Task EnsureUserCannotBeAdminOrOwnerForMultipleFreeOrganizationAsync(OrganizationUser updatedOrgUser, OrganizationUserType existingUserType, Entities.Organization organization)
    {

        if (organization.PlanType != PlanType.Free)
        {
            return;
        }
        if (!updatedOrgUser.UserId.HasValue)
        {
            return;
        }
        if (updatedOrgUser.Type is not (OrganizationUserType.Admin or OrganizationUserType.Owner))
        {
            return;
        }

        // Since free organizations only supports a few users there is not much point in avoiding N+1 queries for this.
        var adminCount = await _organizationUserRepository.GetCountByFreeOrganizationAdminUserAsync(updatedOrgUser.UserId!.Value);

        var isCurrentAdminOrOwner = existingUserType is OrganizationUserType.Admin or OrganizationUserType.Owner;

        if (isCurrentAdminOrOwner && adminCount <= 1)
        {
            return;
        }

        if (!isCurrentAdminOrOwner && adminCount == 0)
        {
            return;
        }

        throw new BadRequestException("User can only be an admin of one free organization.");
    }

    private async Task<List<CollectionAccessSelection>> ValidateAccessAndFilterDefaultUserCollectionsAsync(
        OrganizationUser originalUser, List<CollectionAccessSelection> collectionAccess)
    {
        var collections = await _collectionRepository
            .GetManyByManyIdsAsync(collectionAccess.Select(c => c.Id));

        ValidateCollections(originalUser, collectionAccess, collections);

        return ExcludeDefaultUserCollections(collectionAccess, collections);
    }

    private static void ValidateCollections(OrganizationUser originalUser, List<CollectionAccessSelection> collectionAccess, ICollection<Collection> collections)
    {
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

    private static List<CollectionAccessSelection> ExcludeDefaultUserCollections(
        List<CollectionAccessSelection> collectionAccess, ICollection<Collection> collections) =>
            collectionAccess
                .Where(cas => collections.Any(c => c.Id == cas.Id && c.Type != CollectionType.DefaultUserCollection))
                .ToList();

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
