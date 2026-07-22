using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationUserAction;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using static Bit.Core.AdminConsole.Utilities.v2.Validation.ValidationResultHelpers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.UpdateUser.v2;

public class UpdateOrganizationUserValidator(
    IGroupRepository groupRepository,
    IHasConfirmedOwnersExceptQuery hasConfirmedOwnersExceptQuery,
    IOrganizationUserValidationService organizationUserValidationService,
    IGetOrganizationUsersClaimedStatusQuery getOrganizationUsersClaimedStatusQuery,
    IOrganizationDomainRepository organizationDomainRepository)
    : IUpdateOrganizationUserValidator
{
    public async Task<ValidationResult<UpdateOrganizationUserRequest>> ValidateAsync(
        UpdateOrganizationUserRequest request)
    {
        var organizationUser = request.OrganizationUserToUpdate;

        if (organizationUser.Id == Guid.Empty)
        {
            return Invalid(request, new InviteUserFirst());
        }

        var freeOrgAdminError = await organizationUserValidationService.ValidateFreeOrgAdminLimitAsync(
            organizationUser.UserId, request.Organization.PlanType, organizationUser.Type, request.NewType);

        if (freeOrgAdminError is not null)
        {
            return Invalid(request, freeOrgAdminError);
        }

        var collectionAccessToSave = request.Collections.collectionAccessToSave ?? [];
        if (collectionAccessToSave.Count != 0)
        {
            if (!CollectionsAreValid(collectionAccessToSave, request.Collections.collectionsToSave, organizationUser.OrganizationId))
            {
                return Invalid(request, new CollectionNotFound());
            }

            if (ContainsDefaultUserCollection(collectionAccessToSave, request.Collections.collectionsToSave))
            {
                return Invalid(request, new CannotAssignDefaultCollection());
            }
        }

        if (request.NewGroups?.Any() == true)
        {
            var groupAccess = request.NewGroups.ToList();
            var groups = await groupRepository.GetManyByManyIds(groupAccess);
            if (!GroupsAreValid(groupAccess, groups, organizationUser.OrganizationId))
            {
                return Invalid(request, new GroupNotFound());
            }
        }

        var roleChangeError = await ValidateRoleChangeAsync(request);
        if (roleChangeError is not null)
        {
            return Invalid(request, roleChangeError);
        }

        // Ensure the organization's plan supports custom permissions.
        if (request is { NewType: OrganizationUserType.Custom, Organization.UseCustomPermissions: false })
        {
            return Invalid(request, new CustomPermissionsNotEnabled());
        }

        if (request.NewType != OrganizationUserType.Owner &&
            !await hasConfirmedOwnersExceptQuery.HasConfirmedOwnersExceptAsync(organizationUser.OrganizationId,
                [organizationUser.Id]))
        {
            return Invalid(request, new MustHaveConfirmedOwner());
        }

        if (collectionAccessToSave.Count > 0 && collectionAccessToSave.Any(cas => !cas.Valid()))
        {
            return Invalid(request, new ManageMutuallyExclusive());
        }

        var emailChangeError = await ValidateEmailChangeAsync(request);
        if (emailChangeError is not null)
        {
            return Invalid(request, emailChangeError);
        }

        return Valid(request);
    }

    /// <summary>
    /// A member's email may only be changed when they are claimed by the organization, have no master
    /// password, and the new email is on a domain the organization has verified. Returns null when no
    /// email change is requested or the email is unchanged.
    /// </summary>
    private async Task<Error?> ValidateEmailChangeAsync(UpdateOrganizationUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.NewEmail))
        {
            return null;
        }

        var organizationUser = request.OrganizationUserToUpdate;

        if (request.UserToUpdate is null || !organizationUser.UserId.HasValue)
        {
            return new MemberNotClaimedError();
        }

        if (string.Equals(request.UserToUpdate.Email, request.NewEmail, StringComparison.InvariantCultureIgnoreCase))
        {
            return null;
        }

        if (request.UserToUpdate.HasMasterPassword())
        {
            return new MemberHasMasterPasswordError();
        }

        var claimedStatus = await getOrganizationUsersClaimedStatusQuery
            .GetUsersOrganizationClaimedStatusAsync(request.Organization.Id, [organizationUser.Id]);
        if (!claimedStatus.TryGetValue(organizationUser.Id, out var isClaimed) || !isClaimed)
        {
            return new MemberNotClaimedError();
        }

        var newDomain = EmailValidation.GetDomain(request.NewEmail);
        var verifiedDomains = await organizationDomainRepository
            .GetVerifiedDomainsByOrganizationIdsAsync([request.Organization.Id]);
        if (!verifiedDomains.Any(d => string.Equals(d.DomainName, newDomain, StringComparison.InvariantCultureIgnoreCase)))
        {
            return new NewEmailDomainNotClaimedError();
        }

        return null;
    }

    /// <summary>
    /// Delegates the role-change authority decision to
    /// <see cref="IOrganizationUserValidationService.CanManageRoleChangeAsync"/>. System users skip the check.
    /// </summary>
    private async Task<Error?> ValidateRoleChangeAsync(UpdateOrganizationUserRequest request)
    {
        if (request.PerformedBy is not StandardUser standardUser)
        {
            return null;
        }

        var actingUser = new OrganizationUserRole(
            standardUser.OrganizationUserType!.Value,
            request.OrganizationUserToUpdate.OrganizationId,
            standardUser.Permissions);

        var newTargetUser = new OrganizationUserRole(
            request.NewType,
            request.OrganizationUserToUpdate.OrganizationId,
            request.NewPermissions);

        return await organizationUserValidationService.CanManageRoleChangeAsync(
            standardUser.UserId!.Value,
            actingUser,
            request.OrganizationUserToUpdate,
            newTargetUser);
    }

    private static bool CollectionsAreValid(List<CollectionAccessSelection> collectionAccessToSave,
        ICollection<Collection> collectionsToSave, Guid organizationId)
    {
        var collectionIds = collectionsToSave.Select(c => c.Id);

        var missingCollection = collectionAccessToSave.FirstOrDefault(cas => !collectionIds.Contains(cas.Id));

        return missingCollection == null && collectionsToSave.All(c => c.OrganizationId == organizationId);
    }

    private static bool ContainsDefaultUserCollection(
        List<CollectionAccessSelection> collectionAccessToSave, ICollection<Collection> collectionsToSave) =>
        collectionAccessToSave
            .Any(cas => collectionsToSave.Any(c => c.Id == cas.Id && c.Type == CollectionType.DefaultUserCollection));

    private static bool GroupsAreValid(ICollection<Guid> groupAccess, ICollection<Group> groups, Guid organizationId)
    {
        var groupIds = groups.Select(g => g.Id);

        var missingGroupId = groupAccess.FirstOrDefault(gId => !groupIds.Contains(gId));

        return missingGroupId == Guid.Empty && groups.All(g => g.OrganizationId == organizationId);
    }
}
