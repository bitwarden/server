using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
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

    public UpdateOrganizationUserCommand(
        IEventService eventService,
        IOrganizationService organizationService,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICountNewSmSeatsRequiredQuery countNewSmSeatsRequiredQuery,
        IUpdateSecretsManagerSubscriptionCommand updateSecretsManagerSubscriptionCommand)
    {
        _eventService = eventService;
        _organizationService = organizationService;
        _organizationRepository = organizationRepository;
        _organizationUserRepository = organizationUserRepository;
        _countNewSmSeatsRequiredQuery = countNewSmSeatsRequiredQuery;
        _updateSecretsManagerSubscriptionCommand = updateSecretsManagerSubscriptionCommand;
    }

    public async Task UpdateUserAsync(OrganizationUser user, Guid? savingUserId,
        IEnumerable<CollectionAccessSelection> collections, IEnumerable<Guid> groups)
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
            await _organizationService.ValidateOrganizationUserUpdatePermissions(user.OrganizationId, user.Type, originalUser.Type, user.GetPermissions());
        }

        await _organizationService.ValidateOrganizationCustomPermissionsEnabledAsync(user.OrganizationId, user.Type);

        if (user.Type != OrganizationUserType.Owner &&
            !await _organizationService.HasConfirmedOwnersExceptAsync(user.OrganizationId, new[] { user.Id }))
        {
            throw new BadRequestException("Organization must have at least one confirmed owner.");
        }

        // If the organization is using Flexible Collections, prevent use of any deprecated permissions
        var organization = await _organizationRepository.GetByIdAsync(user.OrganizationId);
        if (organization.FlexibleCollections && user.Type == OrganizationUserType.Manager)
        {
            throw new BadRequestException("The Manager role has been deprecated by collection enhancements. Use the collection Can Manage permission instead.");
        }

        if (organization.FlexibleCollections && user.AccessAll)
        {
            throw new BadRequestException("The AccessAll property has been deprecated by collection enhancements. Assign the user to collections instead.");
        }

        if (organization.FlexibleCollections && collections?.Any() == true)
        {
            var invalidAssociations = collections.Where(cas => cas.Manage && (cas.ReadOnly || cas.HidePasswords));
            if (invalidAssociations.Any())
            {
                throw new BadRequestException("The Manage property is mutually exclusive and cannot be true while the ReadOnly or HidePasswords properties are also true.");
            }
        }
        // End Flexible Collections

        // Only autoscale (if required) after all validation has passed so that we know it's a valid request before
        // updating Stripe
        if (!originalUser.AccessSecretsManager && user.AccessSecretsManager)
        {
            var additionalSmSeatsRequired = await _countNewSmSeatsRequiredQuery.CountNewSmSeatsRequiredAsync(user.OrganizationId, 1);
            if (additionalSmSeatsRequired > 0)
            {
                var update = new SecretsManagerSubscriptionUpdate(organization, true)
                    .AdjustSeats(additionalSmSeatsRequired);
                await _updateSecretsManagerSubscriptionCommand.UpdateSubscriptionAsync(update);
            }
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
