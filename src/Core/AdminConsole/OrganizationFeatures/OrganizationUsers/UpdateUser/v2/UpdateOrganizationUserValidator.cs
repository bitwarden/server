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
    ICollectionRepository collectionRepository,
    IGetOrganizationUsersClaimedStatusQuery getOrganizationUsersClaimedStatusQuery,
    IOrganizationDomainRepository organizationDomainRepository,
    IUserRepository userRepository,
    IOrganizationUserRepository organizationUserRepository)
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

        var collectionAccessToSave = request.CollectionsToSave ?? [];
        if (collectionAccessToSave.Count != 0)
        {
            var collectionsToSave = await collectionRepository.GetManyByManyIdsAsync(collectionAccessToSave.Select(cas => cas.Id));

            if (!CollectionsAreValid(collectionAccessToSave, collectionsToSave, organizationUser.OrganizationId))
            {
                return Invalid(request, new CollectionNotFound());
            }

            if (ContainsDefaultUserCollection(collectionAccessToSave, collectionsToSave))
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

        // Granting PAM access to a member of an organization without PAM would be inert: claim emission ANDs
        // AccessPam with the organization's UsePam. Reject so the admin gets an actionable error instead.
        if (request.IsEnablingPam() && !request.Organization.UsePam)
        {
            return Invalid(request, new PamNotEnabled());
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

        var accountChangeError = await ValidateAccountChangeAsync(request);
        if (accountChangeError is not null)
        {
            return Invalid(request, accountChangeError);
        }

        return Valid(request);
    }

    /// <summary>
    /// A member must be claimed by the organization to change their name; changing their email additionally
    /// requires no master password and a new address on an org-verified domain.
    /// </summary>
    private async Task<Error?> ValidateAccountChangeAsync(UpdateOrganizationUserRequest request)
    {
        var organizationUser = request.OrganizationUserToUpdate;

        // A member with no linked account cannot be claimed, so their email cannot be changed.
        if (!string.IsNullOrWhiteSpace(request.NewEmail) && (request.UserToUpdate is null || !organizationUser.UserId.HasValue))
        {
            return new MemberNotClaimedError();
        }

        var isEmailChanging = request.IsEmailChanged();
        var isNameChanging = request.IsNameChanged();

        if (!isEmailChanging && !isNameChanging)
        {
            return null;
        }

        if (isEmailChanging && request.UserToUpdate!.HasMasterPassword())
        {
            return new MemberHasMasterPasswordError();
        }

        var claimedStatus = await getOrganizationUsersClaimedStatusQuery.GetUsersOrganizationClaimedStatusAsync(request.Organization.Id, [organizationUser.Id]);
        if (!(claimedStatus.TryGetValue(organizationUser.Id, out var isClaimed) && isClaimed))
        {
            return isEmailChanging ? new MemberNotClaimedError() : new NameChangeMemberNotClaimedError();
        }

        if (isEmailChanging)
        {
            var newDomain = EmailValidation.GetDomain(request.NewEmail!);
            var verifiedDomains = await organizationDomainRepository
                .GetVerifiedDomainsByOrganizationIdsAsync([request.Organization.Id]);
            if (!verifiedDomains.Any(d => string.Equals(d.DomainName, newDomain, StringComparison.InvariantCultureIgnoreCase)))
            {
                return new NewEmailDomainNotClaimedError();
            }

            return await ValidateNewEmailNotTakenAsync(request);
        }

        return null;
    }

    /// <summary>
    /// Rejects a new email already owned by another account: <see cref="EmailAlreadyInUseByAnotherMemberError"/>
    /// when that account is a member of this organization, otherwise <see cref="EmailTakenOutsideOrganizationError"/>.
    /// </summary>
    private async Task<Error?> ValidateNewEmailNotTakenAsync(UpdateOrganizationUserRequest request)
    {
        var existingUser = await userRepository.GetByEmailAsync(request.NewEmail!);
        if (existingUser is null || existingUser.Id == request.UserToUpdate!.Id)
        {
            return null;
        }

        var membership = await organizationUserRepository
            .GetByOrganizationAsync(request.Organization.Id, existingUser.Id);

        return membership is not null
            ? new EmailAlreadyInUseByAnotherMemberError()
            : new EmailTakenOutsideOrganizationError();
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
