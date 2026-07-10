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
            .GetUsersOrganizationClaimedStatusAsync(default, default);
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
        Assert.IsType<MemberNotClaimedError>(result.AsError);
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
        Assert.IsType<MemberHasMasterPasswordError>(result.AsError);
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
        Assert.IsType<MemberNotClaimedError>(result.AsError);
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
        Assert.IsType<NewEmailDomainNotClaimedError>(result.AsError);
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
        User userToUpdate = null)
    {
        // IOrganizationUserValidationService is auto-mocked; an unstubbed CanManageRoleChange returns null
        // ("allowed"), so the escalation check passes by default. Its role rules have their own unit tests.

        // Default to a state where validation passes unless a test overrides it.
        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(organizationUser.OrganizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(true);

        // When a test doesn't specify an actor, default to an authorized owner member so unrelated rules can
        // be exercised. The actor's role and permissions travel on the acting user itself.
        var actingUser = performedBy ?? new StandardUser(Guid.NewGuid(), true, OrganizationUserType.Owner);

        return new UpdateOrganizationUserRequest(
            organizationUser,
            organization ?? CreateOrganization(organizationUser.OrganizationId, PlanType.EnterpriseAnnually),
            newType,
            newPermissions,
            false,
            // Default every access selection to an existing shared collection so validation passes; tests override collectionsToSave.
            (collectionsToSave ?? (collectionAccessToSave ?? [])
                .Select(c => new Collection
                {
                    Id = c.Id,
                    OrganizationId = organizationUser.OrganizationId,
                    Type = CollectionType.SharedCollection
                })
                .ToList(), collectionAccessToSave ?? []),
            groups,
            newEmail,
            null,
            actingUser,
            actingMembership,
            userToUpdate);
    }

    private static Organization CreateOrganization(Guid id, PlanType planType, bool useCustomPermissions = true) =>
        new() { Id = id, PlanType = planType, UseCustomPermissions = useCustomPermissions };
}
