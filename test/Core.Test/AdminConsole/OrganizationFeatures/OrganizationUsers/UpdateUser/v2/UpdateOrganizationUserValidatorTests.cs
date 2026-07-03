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
using Bit.Core.Models.Data.Organizations;
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
        var ability = CreateAbility(orgUser.OrganizationId, allowAdminAccessToAllCollectionItems: false);

        var request = CreateRequest(sutProvider, orgUser, OrganizationUserType.User,
            performedBy: performedBy,
            ability: ability,
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
        // Demoting the org's last confirmed owner (Owner -> User).
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
        var ability = CreateAbility(orgUser.OrganizationId, allowAdminAccessToAllCollectionItems: true);
        var request = CreateRequest(sutProvider, orgUser, OrganizationUserType.User,
            ability: ability,
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
        var ability = CreateAbility(orgUser.OrganizationId, allowAdminAccessToAllCollectionItems: true);
        var request = CreateRequest(sutProvider, orgUser, OrganizationUserType.User,
            ability: ability,
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
    public async Task ValidateAsync_WhenNonOwnerPromotesUserToOwner_ReturnsOnlyOwnersCanManageOwners(
        SutProvider<UpdateOrganizationUserValidator> sutProvider,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser)
    {
        var performedBy = new StandardUser(Guid.NewGuid(), isOrganizationOwner: false);
        var request = CreateRequest(sutProvider, orgUser, OrganizationUserType.Owner,
            performedBy: performedBy,
            performedByOrganizationUser: ActingOrganizationUser(OrganizationUserType.Admin));

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<OnlyOwnersCanManageOwners>(result.AsError);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WhenNonOwnerModifiesExistingOwner_ReturnsOnlyOwnersCanManageOwners(
        SutProvider<UpdateOrganizationUserValidator> sutProvider,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser orgUser)
    {
        var performedBy = new StandardUser(Guid.NewGuid(), isOrganizationOwner: false);
        var request = CreateRequest(sutProvider, orgUser, OrganizationUserType.Admin,
            performedBy: performedBy,
            performedByOrganizationUser: ActingOrganizationUser(OrganizationUserType.Admin));

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<OnlyOwnersCanManageOwners>(result.AsError);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WhenCustomUserPromotesUserToAdmin_ReturnsCustomUsersCannotManageAdminsOrOwners(
        SutProvider<UpdateOrganizationUserValidator> sutProvider,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser)
    {
        var performedBy = new StandardUser(Guid.NewGuid(), isOrganizationOwner: false);
        var request = CreateRequest(sutProvider, orgUser, OrganizationUserType.Admin,
            performedBy: performedBy,
            performedByOrganizationUser: ActingOrganizationUser(OrganizationUserType.Custom));

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<CustomUsersCannotManageAdminsOrOwners>(result.AsError);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WhenCustomUserModifiesExistingAdmin_ReturnsCustomUsersCannotManageAdminsOrOwners(
        SutProvider<UpdateOrganizationUserValidator> sutProvider,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Admin)] OrganizationUser orgUser)
    {
        var performedBy = new StandardUser(Guid.NewGuid(), isOrganizationOwner: false);
        var request = CreateRequest(sutProvider, orgUser, OrganizationUserType.User,
            performedBy: performedBy,
            performedByOrganizationUser: ActingOrganizationUser(OrganizationUserType.Custom));

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<CustomUsersCannotManageAdminsOrOwners>(result.AsError);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WhenOwnerPromotesUserToOwner_ReturnsValid(
        SutProvider<UpdateOrganizationUserValidator> sutProvider,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser)
    {
        var performedBy = new StandardUser(Guid.NewGuid(), isOrganizationOwner: true);
        var request = CreateRequest(sutProvider, orgUser, OrganizationUserType.Owner,
            performedBy: performedBy,
            performedByOrganizationUser: ActingOrganizationUser(OrganizationUserType.Owner));

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsValid);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WhenAdminPromotesUserToAdmin_ReturnsValid(
        SutProvider<UpdateOrganizationUserValidator> sutProvider,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser)
    {
        var performedBy = new StandardUser(Guid.NewGuid(), isOrganizationOwner: false);
        var request = CreateRequest(sutProvider, orgUser, OrganizationUserType.Admin,
            performedBy: performedBy,
            performedByOrganizationUser: ActingOrganizationUser(OrganizationUserType.Admin));

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsValid);
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

    private static UpdateOrganizationUserValidationRequest CreateRequest(
        SutProvider<UpdateOrganizationUserValidator> sutProvider,
        OrganizationUser organizationUser,
        OrganizationUserType newType,
        IActingUser performedBy = null,
        OrganizationUser performedByOrganizationUser = null,
        Organization organization = null,
        OrganizationAbility ability = null,
        List<CollectionAccessSelection> collections = null,
        IEnumerable<Guid> groups = null,
        HashSet<Guid> currentAccessIds = null,
        ICollection<Collection> postedCollections = null)
    {
        // Use the real role-validation service so the escalation check exercises its actual rules. The
        // dependency is set under the constructor parameter name because BitAutoData has already stored an
        // auto-mock under that name, which the SutProvider resolves in preference to a type-only override.
        sutProvider.SetDependency<IOrganizationUserValidationService>(
            new OrganizationUserValidationService(), "organizationUserValidationService");
        sutProvider.Create();

        // Default to a state where validation passes unless a test overrides it.
        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(organizationUser.OrganizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(true);

        return new UpdateOrganizationUserValidationRequest(
            organizationUser,
            newType,
            performedBy ?? new StandardUser(Guid.NewGuid(), true),
            performedByOrganizationUser,
            collections ?? [],
            groups,
            currentAccessIds ?? [],
            organization ?? CreateOrganization(organizationUser.OrganizationId, PlanType.EnterpriseAnnually),
            ability ?? CreateAbility(organizationUser.OrganizationId, allowAdminAccessToAllCollectionItems: true),
            // By default, treat every posted collection as an existing shared collection in the org so
            // validation passes; tests override this to exercise missing or default collections.
            postedCollections ?? (collections ?? [])
                .Select(c => new Collection
                {
                    Id = c.Id,
                    OrganizationId = organizationUser.OrganizationId,
                    Type = CollectionType.SharedCollection
                })
                .ToList());
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

    private static OrganizationAbility CreateAbility(Guid id, bool allowAdminAccessToAllCollectionItems) =>
        new() { Id = id, AllowAdminAccessToAllCollectionItems = allowAdminAccessToAllCollectionItems };
}
