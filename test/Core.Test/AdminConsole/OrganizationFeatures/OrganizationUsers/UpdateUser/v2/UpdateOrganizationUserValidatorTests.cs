using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationUserAction;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.UpdateUser.v2;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Test.AutoFixture.OrganizationUserFixtures;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;
using Collection = Bit.Core.Entities.Collection;
using Organization = Bit.Core.AdminConsole.Entities.Organization;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers.UpdateUser.v2;

[SutProviderCustomize]
public class UpdateOrganizationUserValidatorTests
{
    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WithNoCollectionsOrGroups_ReturnsValid(
        SutProvider<UpdateOrganizationUserValidator> sutProvider,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser)
    {
        var request = CreateRequest(sutProvider, orgUser, OrganizationUserType.User);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsValid);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WithEmptyId_ReturnsInviteUserFirst(
        SutProvider<UpdateOrganizationUserValidator> sutProvider,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser)
    {
        orgUser.Id = Guid.Empty;
        var request = CreateRequest(sutProvider, orgUser, OrganizationUserType.User);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<InviteUserFirst>(result.AsError);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WhenFreeOrgAdminLimitExceeded_ReturnsTheServiceError(
        SutProvider<UpdateOrganizationUserValidator> sutProvider,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser)
    {
        // The free-org admin limit lives in the validation service; the validator just forwards its error.
        var request = CreateRequest(sutProvider, orgUser, OrganizationUserType.Admin);

        sutProvider.GetDependency<IOrganizationUserValidationService>()
            .ValidateFreeOrgAdminLimitAsync(Arg.Any<Guid?>(), Arg.Any<PlanType>(), Arg.Any<OrganizationUserType>(),
                Arg.Any<OrganizationUserType>())
            .Returns(new CannotBeAdminOfMultipleFreeOrganizations());

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<CannotBeAdminOfMultipleFreeOrganizations>(result.AsError);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WhenCollectionDoesNotExist_ReturnsCollectionNotFound(
        SutProvider<UpdateOrganizationUserValidator> sutProvider,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser,
        Guid missingCollectionId)
    {
        var request = CreateRequest(sutProvider, orgUser, OrganizationUserType.User,
            collectionAccessToSave: [new CollectionAccessSelection { Id = missingCollectionId }],
            collectionsToSave: []);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<CollectionNotFound>(result.AsError);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WhenCollectionBelongsToAnotherOrganization_ReturnsCollectionNotFound(
        SutProvider<UpdateOrganizationUserValidator> sutProvider,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser,
        Guid collectionId,
        Guid otherOrganizationId)
    {
        // The collection exists but belongs to a different organization; it must be rejected rather than leaked.
        var request = CreateRequest(sutProvider, orgUser, OrganizationUserType.User,
            collectionAccessToSave: [new CollectionAccessSelection { Id = collectionId }],
            collectionsToSave:
            [
                new Collection { Id = collectionId, OrganizationId = otherOrganizationId, Type = CollectionType.SharedCollection }
            ]);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<CollectionNotFound>(result.AsError);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WhenGroupDoesNotExist_ReturnsGroupNotFound(
        SutProvider<UpdateOrganizationUserValidator> sutProvider,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser,
        Guid missingGroupId)
    {
        var request = CreateRequest(sutProvider, orgUser, OrganizationUserType.User,
            groups: [missingGroupId]);

        sutProvider.GetDependency<IGroupRepository>()
            .GetManyByManyIds(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<Group>());

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<GroupNotFound>(result.AsError);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WhenGroupBelongsToAnotherOrganization_ReturnsGroupNotFound(
        SutProvider<UpdateOrganizationUserValidator> sutProvider,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser,
        Guid groupId,
        Guid otherOrganizationId)
    {
        // The group exists but belongs to a different organization; it must be rejected rather than leaked.
        var request = CreateRequest(sutProvider, orgUser, OrganizationUserType.User,
            groups: [groupId]);

        sutProvider.GetDependency<IGroupRepository>()
            .GetManyByManyIds(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<Group> { new() { Id = groupId, OrganizationId = otherOrganizationId } });

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<GroupNotFound>(result.AsError);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WhenCustomTypeAndCustomPermissionsDisabled_ReturnsCustomPermissionsNotEnabled(
        SutProvider<UpdateOrganizationUserValidator> sutProvider,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser)
    {
        var organization = CreateOrganization(orgUser.OrganizationId, PlanType.EnterpriseAnnually, useCustomPermissions: false);
        var request = CreateRequest(sutProvider, orgUser, OrganizationUserType.Custom, organization: organization);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<CustomPermissionsNotEnabled>(result.AsError);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WhenGrantingPamAndOrganizationDoesNotUsePam_ReturnsPamNotEnabled(
        SutProvider<UpdateOrganizationUserValidator> sutProvider,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser)
    {
        orgUser.AccessPam = false;
        var organization = CreateOrganization(orgUser.OrganizationId, PlanType.EnterpriseAnnually, usePam: false);
        var request = CreateRequest(sutProvider, orgUser, OrganizationUserType.User, organization: organization,
            newAccessPam: true);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<PamNotEnabled>(result.AsError);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WhenGrantingPamAndOrganizationUsesPam_ReturnsValid(
        SutProvider<UpdateOrganizationUserValidator> sutProvider,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser)
    {
        orgUser.AccessPam = false;
        var organization = CreateOrganization(orgUser.OrganizationId, PlanType.EnterpriseAnnually, usePam: true);
        var request = CreateRequest(sutProvider, orgUser, OrganizationUserType.User, organization: organization,
            newAccessPam: true);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsValid);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WhenRevokingPamAndOrganizationDoesNotUsePam_ReturnsValid(
        SutProvider<UpdateOrganizationUserValidator> sutProvider,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser)
    {
        // Revoking access must stay possible on an organization whose PAM entitlement has lapsed.
        orgUser.AccessPam = true;
        var organization = CreateOrganization(orgUser.OrganizationId, PlanType.EnterpriseAnnually, usePam: false);
        var request = CreateRequest(sutProvider, orgUser, OrganizationUserType.User, organization: organization,
            newAccessPam: false);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsValid);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WhenMemberAlreadyHasPamAndOrganizationDoesNotUsePam_ReturnsValid(
        SutProvider<UpdateOrganizationUserValidator> sutProvider,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser)
    {
        // Not a grant, so an unrelated edit to a member who already has access is not blocked.
        orgUser.AccessPam = true;
        var organization = CreateOrganization(orgUser.OrganizationId, PlanType.EnterpriseAnnually, usePam: false);
        var request = CreateRequest(sutProvider, orgUser, OrganizationUserType.User, organization: organization,
            newAccessPam: true);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsValid);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WhenRemovingLastConfirmedOwner_ReturnsMustHaveConfirmedOwner(
        SutProvider<UpdateOrganizationUserValidator> sutProvider,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser orgUser)
    {
        var request = CreateRequest(sutProvider, orgUser, OrganizationUserType.User);

        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(orgUser.OrganizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(false);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<MustHaveConfirmedOwner>(result.AsError);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WhenManageCombinedWithReadOnly_ReturnsManageMutuallyExclusive(
        SutProvider<UpdateOrganizationUserValidator> sutProvider,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser,
        Guid collectionId)
    {
        var request = CreateRequest(sutProvider, orgUser, OrganizationUserType.User,
            collectionAccessToSave: [new CollectionAccessSelection { Id = collectionId, Manage = true, ReadOnly = true }]);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<ManageMutuallyExclusive>(result.AsError);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WhenAssigningDefaultUserCollection_ReturnsCannotAssignDefaultCollection(
        SutProvider<UpdateOrganizationUserValidator> sutProvider,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser,
        Guid sharedCollectionId,
        Guid defaultCollectionId)
    {
        var request = CreateRequest(sutProvider, orgUser, OrganizationUserType.User,
            collectionAccessToSave:
            [
                new CollectionAccessSelection { Id = sharedCollectionId },
                new CollectionAccessSelection { Id = defaultCollectionId }
            ],
            collectionsToSave:
            [
                new Collection { Id = sharedCollectionId, OrganizationId = orgUser.OrganizationId, Type = CollectionType.SharedCollection },
                new Collection { Id = defaultCollectionId, OrganizationId = orgUser.OrganizationId, Type = CollectionType.DefaultUserCollection }
            ]);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<CannotAssignDefaultCollection>(result.AsError);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WhenRoleChangeIsDenied_ReturnsTheServiceError(
        SutProvider<UpdateOrganizationUserValidator> sutProvider,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser)
    {
        // The escalation decision (and which error to return) lives in the validation service; the validator
        // just forwards whatever it returns. The mapping itself is covered by the service's own unit tests.
        var request = CreateRequest(sutProvider, orgUser, OrganizationUserType.Admin,
            performedBy: new StandardUser(Guid.NewGuid(), isOrganizationOwner: false, OrganizationUserType.Custom));

        sutProvider.GetDependency<IOrganizationUserValidationService>()
            .CanManageRoleChangeAsync(Arg.Any<Guid>(), Arg.Any<IOrganizationUserRole>(), Arg.Any<IOrganizationUserRole>(),
                Arg.Any<IOrganizationUserRole>())
            .Returns(new CustomUsersCannotManageAdminsOrOwners());

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<CustomUsersCannotManageAdminsOrOwners>(result.AsError);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WhenSystemUserPromotesToOwner_SkipsEscalationCheck(
        SutProvider<UpdateOrganizationUserValidator> sutProvider,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser)
    {
        var request = CreateRequest(sutProvider, orgUser, OrganizationUserType.Owner,
            performedBy: new SystemUser(EventSystemUser.SCIM));

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsValid);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WhenEmailUnchanged_ReturnsValidAndSkipsEmailChecks(
        SutProvider<UpdateOrganizationUserValidator> sutProvider,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser)
    {
        orgUser.UserId = Guid.NewGuid();
        var userToUpdate = new User { Id = orgUser.UserId!.Value, Email = "member@claimed.example.com" };
        var request = CreateRequest(sutProvider, orgUser, OrganizationUserType.User,
            newEmail: "MEMBER@claimed.example.com", userToUpdate: userToUpdate);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsValid);
        await sutProvider.GetDependency<IGetOrganizationUsersClaimedStatusQuery>()
            .DidNotReceiveWithAnyArgs()
            .GetUsersOrganizationClaimedStatusAsync(Arg.Any<Guid>(), Arg.Any<IEnumerable<Guid>>());
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WhenChangingEmailButUserNotLoaded_ReturnsMemberNotClaimed(
        SutProvider<UpdateOrganizationUserValidator> sutProvider,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser)
    {
        // No backing account was loaded (e.g. an invited-but-unconfirmed member), so the email cannot be changed.
        var request = CreateRequest(sutProvider, orgUser, OrganizationUserType.User,
            newEmail: "new@claimed.example.com", userToUpdate: null);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        var error = Assert.IsType<MemberNotClaimedError>(result.AsError);
        Assert.Equal("member_not_claimed", error.Type);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WhenChangingEmailForMemberWithMasterPassword_ReturnsMemberHasMasterPassword(
        SutProvider<UpdateOrganizationUserValidator> sutProvider,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser)
    {
        orgUser.UserId = Guid.NewGuid();
        var userToUpdate = new User
        {
            Id = orgUser.UserId!.Value,
            Email = "member@claimed.example.com",
            MasterPassword = "hashed-master-password"
        };
        var request = CreateRequest(sutProvider, orgUser, OrganizationUserType.User,
            newEmail: "new@claimed.example.com", userToUpdate: userToUpdate);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        var error = Assert.IsType<MemberHasMasterPasswordError>(result.AsError);
        Assert.Equal("member_has_master_password", error.Type);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WhenChangingEmailForUnclaimedMember_ReturnsMemberNotClaimed(
        SutProvider<UpdateOrganizationUserValidator> sutProvider,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser)
    {
        orgUser.UserId = Guid.NewGuid();
        var userToUpdate = new User { Id = orgUser.UserId!.Value, Email = "member@claimed.example.com" };
        var request = CreateRequest(sutProvider, orgUser, OrganizationUserType.User,
            newEmail: "new@claimed.example.com", userToUpdate: userToUpdate);

        sutProvider.GetDependency<IGetOrganizationUsersClaimedStatusQuery>()
            .GetUsersOrganizationClaimedStatusAsync(orgUser.OrganizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(new Dictionary<Guid, bool> { [orgUser.Id] = false });

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        var error = Assert.IsType<MemberNotClaimedError>(result.AsError);
        Assert.Equal("member_not_claimed", error.Type);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WhenNewEmailDomainNotVerified_ReturnsNewEmailDomainNotClaimed(
        SutProvider<UpdateOrganizationUserValidator> sutProvider,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser)
    {
        orgUser.UserId = Guid.NewGuid();
        var userToUpdate = new User { Id = orgUser.UserId!.Value, Email = "member@claimed.example.com" };
        var request = CreateRequest(sutProvider, orgUser, OrganizationUserType.User,
            newEmail: "new@unclaimed.example.com", userToUpdate: userToUpdate);

        sutProvider.GetDependency<IGetOrganizationUsersClaimedStatusQuery>()
            .GetUsersOrganizationClaimedStatusAsync(orgUser.OrganizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(new Dictionary<Guid, bool> { [orgUser.Id] = true });
        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .GetVerifiedDomainsByOrganizationIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<OrganizationDomain> { new() { DomainName = "claimed.example.com" } });

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        var error = Assert.IsType<NewEmailDomainNotClaimedError>(result.AsError);
        Assert.Equal("new_email_domain_not_claimed", error.Type);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WhenChangingEmailForClaimedNoMasterPasswordMemberOnVerifiedDomain_ReturnsValid(
        SutProvider<UpdateOrganizationUserValidator> sutProvider,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser)
    {
        orgUser.UserId = Guid.NewGuid();
        var userToUpdate = new User { Id = orgUser.UserId!.Value, Email = "member@claimed.example.com" };
        var request = CreateRequest(sutProvider, orgUser, OrganizationUserType.User,
            newEmail: "new@claimed.example.com", userToUpdate: userToUpdate);

        sutProvider.GetDependency<IGetOrganizationUsersClaimedStatusQuery>()
            .GetUsersOrganizationClaimedStatusAsync(orgUser.OrganizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(new Dictionary<Guid, bool> { [orgUser.Id] = true });
        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .GetVerifiedDomainsByOrganizationIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<OrganizationDomain> { new() { DomainName = "claimed.example.com" } });

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsValid);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WhenNewEmailTakenByAnotherOrganizationMember_ReturnsEmailAlreadyInUseByAnotherMember(
        SutProvider<UpdateOrganizationUserValidator> sutProvider,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser)
    {
        orgUser.UserId = Guid.NewGuid();
        var userToUpdate = new User { Id = orgUser.UserId!.Value, Email = "member@claimed.example.com" };
        var request = CreateRequest(sutProvider, orgUser, OrganizationUserType.User,
            newEmail: "new@claimed.example.com", userToUpdate: userToUpdate);

        sutProvider.GetDependency<IGetOrganizationUsersClaimedStatusQuery>()
            .GetUsersOrganizationClaimedStatusAsync(orgUser.OrganizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(new Dictionary<Guid, bool> { [orgUser.Id] = true });
        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .GetVerifiedDomainsByOrganizationIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<OrganizationDomain> { new() { DomainName = "claimed.example.com" } });

        var emailOwner = new User { Id = Guid.NewGuid(), Email = "new@claimed.example.com" };
        sutProvider.GetDependency<IUserRepository>()
            .GetByEmailAsync("new@claimed.example.com")
            .Returns(emailOwner);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(Arg.Any<Guid>(), emailOwner.Id)
            .Returns(new OrganizationUser());

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<EmailAlreadyInUseByAnotherMemberError>(result.AsError);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WhenNewEmailTakenOutsideOrganization_ReturnsEmailTakenOutsideOrganization(
        SutProvider<UpdateOrganizationUserValidator> sutProvider,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser)
    {
        orgUser.UserId = Guid.NewGuid();
        var userToUpdate = new User { Id = orgUser.UserId!.Value, Email = "member@claimed.example.com" };
        var request = CreateRequest(sutProvider, orgUser, OrganizationUserType.User,
            newEmail: "new@claimed.example.com", userToUpdate: userToUpdate);

        sutProvider.GetDependency<IGetOrganizationUsersClaimedStatusQuery>()
            .GetUsersOrganizationClaimedStatusAsync(orgUser.OrganizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(new Dictionary<Guid, bool> { [orgUser.Id] = true });
        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .GetVerifiedDomainsByOrganizationIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<OrganizationDomain> { new() { DomainName = "claimed.example.com" } });

        var emailOwner = new User { Id = Guid.NewGuid(), Email = "new@claimed.example.com" };
        sutProvider.GetDependency<IUserRepository>()
            .GetByEmailAsync("new@claimed.example.com")
            .Returns(emailOwner);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<EmailTakenOutsideOrganizationError>(result.AsError);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WhenNewEmailNotTaken_ReturnsValidAndSkipsMembershipLookup(
        SutProvider<UpdateOrganizationUserValidator> sutProvider,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser)
    {
        orgUser.UserId = Guid.NewGuid();
        var userToUpdate = new User { Id = orgUser.UserId!.Value, Email = "member@claimed.example.com" };
        var request = CreateRequest(sutProvider, orgUser, OrganizationUserType.User,
            newEmail: "new@claimed.example.com", userToUpdate: userToUpdate);

        sutProvider.GetDependency<IGetOrganizationUsersClaimedStatusQuery>()
            .GetUsersOrganizationClaimedStatusAsync(orgUser.OrganizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(new Dictionary<Guid, bool> { [orgUser.Id] = true });
        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .GetVerifiedDomainsByOrganizationIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<OrganizationDomain> { new() { DomainName = "claimed.example.com" } });
        // GetByEmailAsync returns null (default) — the new email is free.

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsValid);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WhenChangingNameForClaimedMember_ReturnsValid(
        SutProvider<UpdateOrganizationUserValidator> sutProvider,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser)
    {
        orgUser.UserId = Guid.NewGuid();
        var userToUpdate = new User { Id = orgUser.UserId!.Value, Name = "Old Name" };
        var request = CreateRequest(sutProvider, orgUser, OrganizationUserType.User,
            newName: "New Name", userToUpdate: userToUpdate);

        sutProvider.GetDependency<IGetOrganizationUsersClaimedStatusQuery>()
            .GetUsersOrganizationClaimedStatusAsync(orgUser.OrganizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(new Dictionary<Guid, bool> { [orgUser.Id] = true });

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsValid);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WhenChangingNameForUnclaimedMember_ReturnsMemberNotClaimed(
        SutProvider<UpdateOrganizationUserValidator> sutProvider,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser)
    {
        orgUser.UserId = Guid.NewGuid();
        var userToUpdate = new User { Id = orgUser.UserId!.Value, Name = "Old Name" };
        var request = CreateRequest(sutProvider, orgUser, OrganizationUserType.User,
            newName: "New Name", userToUpdate: userToUpdate);

        sutProvider.GetDependency<IGetOrganizationUsersClaimedStatusQuery>()
            .GetUsersOrganizationClaimedStatusAsync(orgUser.OrganizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(new Dictionary<Guid, bool> { [orgUser.Id] = false });

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        var error = Assert.IsType<NameChangeMemberNotClaimedError>(result.AsError);
        Assert.Equal("name_member_not_claimed", error.Type);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WhenNameUnchanged_ReturnsValidAndSkipsClaimedCheck(
        SutProvider<UpdateOrganizationUserValidator> sutProvider,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser)
    {
        orgUser.UserId = Guid.NewGuid();
        var userToUpdate = new User { Id = orgUser.UserId!.Value, Name = "Same Name" };
        var request = CreateRequest(sutProvider, orgUser, OrganizationUserType.User,
            newName: "Same Name", userToUpdate: userToUpdate);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsValid);
        await sutProvider.GetDependency<IGetOrganizationUsersClaimedStatusQuery>()
            .DidNotReceiveWithAnyArgs()
            .GetUsersOrganizationClaimedStatusAsync(Arg.Any<Guid>(), Arg.Any<IEnumerable<Guid>>());
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WhenChangingEmailAndName_ChecksClaimedStatusOnce(
        SutProvider<UpdateOrganizationUserValidator> sutProvider,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser)
    {
        orgUser.UserId = Guid.NewGuid();
        var userToUpdate = new User
        {
            Id = orgUser.UserId!.Value,
            Email = "member@claimed.example.com",
            Name = "Old Name"
        };
        var request = CreateRequest(sutProvider, orgUser, OrganizationUserType.User,
            newEmail: "new@claimed.example.com", newName: "New Name", userToUpdate: userToUpdate);

        sutProvider.GetDependency<IGetOrganizationUsersClaimedStatusQuery>()
            .GetUsersOrganizationClaimedStatusAsync(orgUser.OrganizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(new Dictionary<Guid, bool> { [orgUser.Id] = true });
        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .GetVerifiedDomainsByOrganizationIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<OrganizationDomain> { new() { DomainName = "claimed.example.com" } });

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsValid);
        await sutProvider.GetDependency<IGetOrganizationUsersClaimedStatusQuery>()
            .Received(1)
            .GetUsersOrganizationClaimedStatusAsync(orgUser.OrganizationId, Arg.Any<IEnumerable<Guid>>());
    }

    private static UpdateOrganizationUserRequest CreateRequest(
        SutProvider<UpdateOrganizationUserValidator> sutProvider,
        OrganizationUser organizationUser,
        OrganizationUserType newType,
        IActingUser performedBy = null,
        Organization organization = null,
        List<CollectionAccessSelection> collectionAccessToSave = null,
        IEnumerable<Guid> groups = null,
        ICollection<Collection> collectionsToSave = null,
        Permissions newPermissions = null,
        string newEmail = null,
        string newName = null,
        User userToUpdate = null,
        bool newAccessPam = false)
    {
        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(organizationUser.OrganizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(true);

        var actingUser = performedBy ?? new StandardUser(Guid.NewGuid(), true, OrganizationUserType.Owner);

        var collectionAccess = collectionAccessToSave ?? [];

        var collections = collectionsToSave ?? collectionAccess
            .Select(c => new Collection
            {
                Id = c.Id,
                OrganizationId = organizationUser.OrganizationId,
                Type = CollectionType.SharedCollection
            })
            .ToList();

        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(collections);

        return new UpdateOrganizationUserRequest(
            organizationUser,
            organization ?? CreateOrganization(organizationUser.OrganizationId, PlanType.EnterpriseAnnually),
            newType,
            newPermissions,
            false,
            newAccessPam,
            collectionAccess,
            groups,
            newEmail,
            newName,
            null,
            actingUser,
            userToUpdate);
    }

    private static Organization CreateOrganization(Guid id, PlanType planType, bool useCustomPermissions = true,
        bool usePam = false) =>
        new() { Id = id, PlanType = planType, UseCustomPermissions = useCustomPermissions, UsePam = usePam };
}
