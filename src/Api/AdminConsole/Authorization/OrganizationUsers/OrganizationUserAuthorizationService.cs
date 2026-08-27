using Bit.Api.AdminConsole.Authorization.Collections;
using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.Context;
using Bit.Core.Repositories;

namespace Bit.Api.AdminConsole.Authorization.OrganizationUsers;

public class OrganizationUserAuthorizationService(
    ICurrentContext currentContext,
    IOrganizationUserRepository organizationUserRepository,
    IOrganizationAbilityCacheService organizationAbilityCacheService,
    ICollectionAuthorizationService collectionAuthorizationService) : IOrganizationUserAuthorizationService
{
    public async Task<OrganizationUserAuthorizationResult> AuthorizeSaveAsync(
        Guid organizationId,
        Guid? organizationUserId,
        IReadOnlyCollection<Guid> postedCollectionIds,
        IReadOnlyCollection<Guid> currentCollectionIds)
    {
        var authorizedPostedCollectionIds =
            await collectionAuthorizationService.AuthorizeModifyUserAccessManyAsync(organizationId, postedCollectionIds);
        var authorizedCurrentCollectionIds =
            await collectionAuthorizationService.AuthorizeModifyUserAccessManyAsync(organizationId, currentCollectionIds);

        var isEditingOwnMembership = await IsEditingOwnMembershipAsync(organizationId, organizationUserId);

        return new OrganizationUserAuthorizationResult(
            CanAddSelfToCollection: !isEditingOwnMembership || postedCollectionIds.All(currentCollectionIds.Contains),
            CanEditOwnGroups: !isEditingOwnMembership,
            postedCollectionIds.Except(authorizedPostedCollectionIds).ToHashSet(),
            currentCollectionIds.Except(authorizedCurrentCollectionIds).ToHashSet());
    }

    /// <summary>
    /// True when the caller is editing their own membership of an organization that does not let admins reach every
    /// collection item. Such a caller cannot give themselves new collection access, or change their own groups.
    /// </summary>
    private async Task<bool> IsEditingOwnMembershipAsync(Guid organizationId, Guid? organizationUserId)
    {
        // An invited user has no organization user yet, and the caller is never the user they invite.
        if (organizationUserId is null || !currentContext.UserId.HasValue)
        {
            return false;
        }

        var organizationAbility = await organizationAbilityCacheService.GetOrganizationAbilityAsync(organizationId);
        if (organizationAbility is { AllowAdminAccessToAllCollectionItems: true })
        {
            return false;
        }

        var targetOrganizationUser = await organizationUserRepository.GetByIdAsync(organizationUserId.Value);
        return targetOrganizationUser?.UserId == currentContext.UserId.Value;
    }
}
