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
    public async Task ValidateAsync_WhenBecomingAdminOfSecondFreeOrg_ReturnsCannotBeAdminOfMultipleFreeOrgs(
        SutProvider<UpdateOrganizationUserValidator> sutProvider,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser)
    {
        orgUser.UserId = Guid.NewGuid();
        var organization = CreateOrganization(orgUser.OrganizationId, PlanType.Free);
        var request = CreateRequest(sutProvider, orgUser, OrganizationUserType.Admin, organization: organization);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetCountByFreeOrganizationAdminUserAsync(orgUser.UserId!.Value)
            .Returns(1);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<CannotBeAdminOfMultipleFreeOrgs>(result.AsError);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WhenEditingSelfAndAddingNewCollection_ReturnsCannotAddSelfToCollection(
        SutProvider<UpdateOrganizationUserValidator> sutProvider,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser,
        Guid newCollectionId)
    {
        orgUser.UserId = Guid.NewGuid();
        var performedBy = new StandardUser(orgUser.UserId!.Value, false);

        var request = CreateRequest(sutProvider, orgUser, OrganizationUserType.User,
            performedBy: performedBy,
            collections: [new CollectionAccessSelection { Id = newCollectionId }],
            currentAccessIds: []);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<CannotAddSelfToCollection>(result.AsError);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WhenCollectionDoesNotExist_ReturnsCollectionNotFound(
        SutProvider<UpdateOrganizationUserValidator> sutProvider,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser,
        Guid missingCollectionId)
    {
        var request = CreateRequest(sutProvider, orgUser, OrganizationUserType.User,
            collections: [new CollectionAccessSelection { Id = missingCollectionId }],
            postedCollections: []);

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
            collections: [new CollectionAccessSelection { Id = collectionId }],
            postedCollections:
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
            collections: [new CollectionAccessSelection { Id = collectionId, Manage = true, ReadOnly = true }]);

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
            collections:
            [
                new CollectionAccessSelection { Id = sharedCollectionId },
                new CollectionAccessSelection { Id = defaultCollectionId }
            ],
            postedCollections:
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
    public async Task ValidateAsync_WhenManagementDeniedAndNoOwnerInvolved_ReturnsCustomUsersCannotManageAdminsOrOwners(
        SutProvider<UpdateOrganizationUserValidator> sutProvider,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser)
    {
        // The role-validation service owns (and independently tests) which actors may manage which roles. The
        // validator's only responsibility is to fail when the service denies management; the happy path is
        // already covered by the other tests, whose default (unstubbed) CanManage returns "allowed".
        // Neither the current (User) nor requested (Admin) role is Owner, so the denial maps to the
        // custom-user error rather than the owner-specific one.
        var request = CreateRequest(sutProvider, orgUser, OrganizationUserType.Admin,
            performedBy: new StandardUser(Guid.NewGuid(), isOrganizationOwner: false),
            performedByOrganizationUser: ActingOrganizationUser(OrganizationUserType.Custom));

        sutProvider.GetDependency<IOrganizationUserValidationService>()
            .CanManage(Arg.Any<Guid>(), Arg.Any<OrganizationUser>(), Arg.Any<OrganizationUser>())
            .Returns(new CannotManageTargetUser());

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<CustomUsersCannotManageAdminsOrOwners>(result.AsError);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WhenManagementDeniedAndTargetIsOwner_ReturnsOnlyOwnersCanManageOwners(
        SutProvider<UpdateOrganizationUserValidator> sutProvider,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser orgUser)
    {
        // The target is currently an Owner, so a denied management attempt maps to the owner-specific error
        // regardless of the requested role.
        var request = CreateRequest(sutProvider, orgUser, OrganizationUserType.User,
            performedBy: new StandardUser(Guid.NewGuid(), isOrganizationOwner: false),
            performedByOrganizationUser: ActingOrganizationUser(OrganizationUserType.Custom));

        sutProvider.GetDependency<IOrganizationUserValidationService>()
            .CanManage(Arg.Any<Guid>(), Arg.Any<OrganizationUser>(), Arg.Any<OrganizationUser>())
            .Returns(new CannotManageTargetUser());

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<OnlyOwnersCanManageOwners>(result.AsError);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WhenManagementDeniedAndPromotingToOwner_ReturnsOnlyOwnersCanManageOwners(
        SutProvider<UpdateOrganizationUserValidator> sutProvider,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser)
    {
        // The requested role is Owner, so a denied management attempt maps to the owner-specific error even
        // though the target's current role (User) is not.
        var request = CreateRequest(sutProvider, orgUser, OrganizationUserType.Owner,
            performedBy: new StandardUser(Guid.NewGuid(), isOrganizationOwner: false),
            performedByOrganizationUser: ActingOrganizationUser(OrganizationUserType.Custom));

        sutProvider.GetDependency<IOrganizationUserValidationService>()
            .CanManage(Arg.Any<Guid>(), Arg.Any<OrganizationUser>(), Arg.Any<OrganizationUser>())
            .Returns(new CannotManageTargetUser());

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<OnlyOwnersCanManageOwners>(result.AsError);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WhenSystemUserPromotesToOwner_SkipsEscalationCheck(
        SutProvider<UpdateOrganizationUserValidator> sutProvider,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser)
    {
        var request = CreateRequest(sutProvider, orgUser, OrganizationUserType.Owner,
            performedBy: new SystemUser(EventSystemUser.SCIM),
            performedByOrganizationUser: null);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsValid);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WhenCustomActorGrantsPermissionTheyDoNotHold_ReturnsCustomUsersCanOnlyGrantOwnPermissions(
        SutProvider<UpdateOrganizationUserValidator> sutProvider,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser)
    {
        // A Custom actor holding only ManageUsers cannot grant a target a permission (ManageSso) it lacks.
        var request = CreateRequest(sutProvider, orgUser, OrganizationUserType.Custom,
            performedBy: new StandardUser(Guid.NewGuid(), isOrganizationOwner: false),
            performedByOrganizationUser: ActingOrganizationUser(OrganizationUserType.Custom),
            newPermissions: new Permissions { ManageSso = true });

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<CustomUsersCanOnlyGrantOwnPermissions>(result.AsError);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WhenCustomActorGrantsOnlyPermissionsTheyHold_ReturnsValid(
        SutProvider<UpdateOrganizationUserValidator> sutProvider,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser)
    {
        // The actor holds ManageUsers and grants only ManageUsers, which is within their own authority.
        var request = CreateRequest(sutProvider, orgUser, OrganizationUserType.Custom,
            performedBy: new StandardUser(Guid.NewGuid(), isOrganizationOwner: false),
            performedByOrganizationUser: ActingOrganizationUser(OrganizationUserType.Custom),
            newPermissions: new Permissions { ManageUsers = true });

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsValid);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WhenOwnerGrantsPermissionsBeyondAnyMember_ReturnsValid(
        SutProvider<UpdateOrganizationUserValidator> sutProvider,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser)
    {
        // An Owner is exempt from the grant-subset check and may grant any custom permission.
        var request = CreateRequest(sutProvider, orgUser, OrganizationUserType.Custom,
            performedBy: new StandardUser(Guid.NewGuid(), isOrganizationOwner: true),
            performedByOrganizationUser: ActingOrganizationUser(OrganizationUserType.Owner),
            newPermissions: new Permissions { ManageScim = true, ManageSso = true, AccessImportExport = true });

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsValid);
    }

    private static UpdateOrganizationUserRequest CreateRequest(
        SutProvider<UpdateOrganizationUserValidator> sutProvider,
        OrganizationUser organizationUser,
        OrganizationUserType newType,
        IActingUser performedBy = null,
        OrganizationUser performedByOrganizationUser = null,
        Organization organization = null,
        List<CollectionAccessSelection> collections = null,
        IEnumerable<Guid> groups = null,
        HashSet<Guid> currentAccessIds = null,
        ICollection<Collection> postedCollections = null,
        Permissions newPermissions = null)
    {
        // IOrganizationUserValidationService is auto-mocked; an unstubbed CanManage returns null ("allowed"),
        // so the escalation check passes by default. Its role rules have their own unit tests.

        // Default to a state where validation passes unless a test overrides it.
        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(organizationUser.OrganizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(true);

        // When a test doesn't specify an actor, default to an authorized owner member so unrelated rules can
        // be exercised. If a test provides its own actor, respect a null membership (e.g. a system user or a
        // provider, whose authority is resolved by the service rather than a membership role).
        var actingUser = performedBy ?? new StandardUser(Guid.NewGuid(), true);
        var actingMembership = performedByOrganizationUser
                               ?? (performedBy is null ? ActingOrganizationUser(OrganizationUserType.Owner) : null);

        return new UpdateOrganizationUserRequest(
            organizationUser,
            organization ?? CreateOrganization(organizationUser.OrganizationId, PlanType.EnterpriseAnnually),
            currentAccessIds ?? [],
            // By default, treat every posted collection as an existing shared collection in the org so
            // validation passes; tests override this to exercise missing or default collections.
            postedCollections ?? (collections ?? [])
                .Select(c => new Collection
                {
                    Id = c.Id,
                    OrganizationId = organizationUser.OrganizationId,
                    Type = CollectionType.SharedCollection
                })
                .ToList(),
            newType,
            newPermissions,
            false,
            collections ?? [],
            groups,
            actingUser,
            actingMembership,
            null);
    }

    // The acting user's own membership. Custom users are given ManageUsers by default, since that is the
    // authority a Custom user needs to act on other members.
    private static OrganizationUser ActingOrganizationUser(OrganizationUserType type, bool manageUsers = true)
    {
        var actingUser = new OrganizationUser { Type = type };
        if (type == OrganizationUserType.Custom)
        {
            actingUser.SetPermissions(new Permissions { ManageUsers = manageUsers });
        }

        return actingUser;
    }

    private static Organization CreateOrganization(Guid id, PlanType planType, bool useCustomPermissions = true) =>
        new() { Id = id, PlanType = planType, UseCustomPermissions = useCustomPermissions };
}
