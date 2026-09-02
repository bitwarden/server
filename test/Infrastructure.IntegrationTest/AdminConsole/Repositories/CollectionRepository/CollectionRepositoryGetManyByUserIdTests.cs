using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Entities;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Repositories.CollectionRepository;

public class CollectionRepositoryGetManyByUserIdTests
{
    /// <summary>
    /// A user assigned directly to a collection gets exactly the permissions on that assignment.
    /// </summary>
    [Theory, DatabaseData]
    public async Task GetManyByUserIdAsync_DirectAssignmentOnly_ReturnsDirectPermissions(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var user = await userRepository.CreateTestUserAsync();
        var orgUser = await organizationUserRepository.CreateTestOrganizationUserAsync(organization, user);

        var collection = new Collection { Name = "Direct only", OrganizationId = organization.Id };
        await collectionRepository.CreateAsync(collection,
            groups: [],
            users:
            [
                new CollectionAccessSelection {Id = orgUser.Id, ReadOnly = true, HidePasswords = true, Manage = false}
            ]);

        var result = await collectionRepository.GetManyByUserIdAsync(user.Id);

        var actual = Assert.Single(result);
        Assert.Equal(collection.Id, actual.Id);
        Assert.True(actual.ReadOnly);
        Assert.True(actual.HidePasswords);
        Assert.False(actual.Manage);
    }

    /// <summary>
    /// A user with no direct assignment inherits the permissions of the group that grants access.
    /// </summary>
    [Theory, DatabaseData]
    public async Task GetManyByUserIdAsync_GroupAssignmentOnly_ReturnsGroupPermissions(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IGroupRepository groupRepository,
        ICollectionRepository collectionRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var user = await userRepository.CreateTestUserAsync();
        var orgUser = await organizationUserRepository.CreateTestOrganizationUserAsync(organization, user);

        var group = await groupRepository.CreateTestGroupAsync(organization);
        await groupRepository.UpdateUsersAsync(group.Id, [orgUser.Id], DateTime.UtcNow);

        var collection = new Collection { Name = "Group only", OrganizationId = organization.Id };
        await collectionRepository.CreateAsync(collection,
            groups:
            [
                new CollectionAccessSelection {Id = group.Id, ReadOnly = true, HidePasswords = true, Manage = false}
            ],
            users: []);

        var result = await collectionRepository.GetManyByUserIdAsync(user.Id);

        var actual = Assert.Single(result);
        Assert.Equal(collection.Id, actual.Id);
        Assert.True(actual.ReadOnly);
        Assert.True(actual.HidePasswords);
        Assert.False(actual.Manage);
    }

    /// <summary>
    /// A direct assignment replaces group permissions entirely, even when the group is more permissive.
    /// The collection must be returned exactly once, with the direct permissions only.
    /// </summary>
    [Theory, DatabaseData]
    public async Task GetManyByUserIdAsync_DirectAndGroupAssignment_DirectAssignmentReplacesGroupPermissions(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IGroupRepository groupRepository,
        ICollectionRepository collectionRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var user = await userRepository.CreateTestUserAsync();
        var orgUser = await organizationUserRepository.CreateTestOrganizationUserAsync(organization, user);

        var group = await groupRepository.CreateTestGroupAsync(organization);
        await groupRepository.UpdateUsersAsync(group.Id, [orgUser.Id], DateTime.UtcNow);

        var collection = new Collection { Name = "Direct overrides group", OrganizationId = organization.Id };
        await collectionRepository.CreateAsync(collection,
            groups:
            [
                // Group grants full manage access
                new CollectionAccessSelection {Id = group.Id, ReadOnly = false, HidePasswords = false, Manage = true}
            ],
            users:
            [
                // Direct assignment is the more restrictive one and must win
                new CollectionAccessSelection {Id = orgUser.Id, ReadOnly = true, HidePasswords = true, Manage = false}
            ]);

        var result = await collectionRepository.GetManyByUserIdAsync(user.Id);

        var actual = Assert.Single(result);
        Assert.Equal(collection.Id, actual.Id);
        Assert.True(actual.ReadOnly);
        Assert.True(actual.HidePasswords);
        Assert.False(actual.Manage);
    }

    /// <summary>
    /// A direct assignment that is more permissive than the group also wins — the group permissions are not
    /// combined in, they are ignored.
    /// </summary>
    [Theory, DatabaseData]
    public async Task GetManyByUserIdAsync_DirectAssignmentMorePermissiveThanGroup_ReturnsDirectPermissions(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IGroupRepository groupRepository,
        ICollectionRepository collectionRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var user = await userRepository.CreateTestUserAsync();
        var orgUser = await organizationUserRepository.CreateTestOrganizationUserAsync(organization, user);

        var group = await groupRepository.CreateTestGroupAsync(organization);
        await groupRepository.UpdateUsersAsync(group.Id, [orgUser.Id], DateTime.UtcNow);

        var collection = new Collection { Name = "Direct is broader", OrganizationId = organization.Id };
        await collectionRepository.CreateAsync(collection,
            groups:
            [
                new CollectionAccessSelection {Id = group.Id, ReadOnly = true, HidePasswords = true, Manage = false}
            ],
            users:
            [
                new CollectionAccessSelection {Id = orgUser.Id, ReadOnly = false, HidePasswords = false, Manage = true}
            ]);

        var result = await collectionRepository.GetManyByUserIdAsync(user.Id);

        var actual = Assert.Single(result);
        Assert.Equal(collection.Id, actual.Id);
        Assert.False(actual.ReadOnly);
        Assert.False(actual.HidePasswords);
        Assert.True(actual.Manage);
    }

    /// <summary>
    /// When multiple groups grant access to the same collection and there is no direct assignment, the effective
    /// permissions are the most permissive combination across those groups.
    /// </summary>
    [Theory, DatabaseData]
    public async Task GetManyByUserIdAsync_MultipleGroupAssignments_ReturnsMostPermissiveCombination(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IGroupRepository groupRepository,
        ICollectionRepository collectionRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var user = await userRepository.CreateTestUserAsync();
        var orgUser = await organizationUserRepository.CreateTestOrganizationUserAsync(organization, user);

        var restrictedGroup = await groupRepository.CreateTestGroupAsync(organization, "restricted");
        var manageGroup = await groupRepository.CreateTestGroupAsync(organization, "manage");
        await groupRepository.UpdateUsersAsync(restrictedGroup.Id, [orgUser.Id], DateTime.UtcNow);
        await groupRepository.UpdateUsersAsync(manageGroup.Id, [orgUser.Id], DateTime.UtcNow);

        var collection = new Collection { Name = "Two groups", OrganizationId = organization.Id };
        await collectionRepository.CreateAsync(collection,
            groups:
            [
                new CollectionAccessSelection
                {
                    Id = restrictedGroup.Id, ReadOnly = true, HidePasswords = true, Manage = false
                },
                new CollectionAccessSelection
                {
                    Id = manageGroup.Id, ReadOnly = false, HidePasswords = false, Manage = true
                }
            ],
            users: []);

        var result = await collectionRepository.GetManyByUserIdAsync(user.Id);

        var actual = Assert.Single(result);
        Assert.Equal(collection.Id, actual.Id);
        Assert.False(actual.ReadOnly);
        Assert.False(actual.HidePasswords);
        Assert.True(actual.Manage);
    }

    /// <summary>
    /// Each collection resolves its own permission source. A direct assignment on one collection must not suppress
    /// group-derived access to a different collection, and vice versa.
    /// </summary>
    [Theory, DatabaseData]
    public async Task GetManyByUserIdAsync_MixOfDirectAndGroupCollections_EachCollectionResolvedIndependently(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IGroupRepository groupRepository,
        ICollectionRepository collectionRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var user = await userRepository.CreateTestUserAsync();
        var orgUser = await organizationUserRepository.CreateTestOrganizationUserAsync(organization, user);

        var group = await groupRepository.CreateTestGroupAsync(organization);
        await groupRepository.UpdateUsersAsync(group.Id, [orgUser.Id], DateTime.UtcNow);

        // Direct assignment only
        var directCollection = new Collection { Name = "Direct", OrganizationId = organization.Id };
        await collectionRepository.CreateAsync(directCollection,
            groups: [],
            users:
            [
                new CollectionAccessSelection {Id = orgUser.Id, ReadOnly = true, HidePasswords = false, Manage = false}
            ]);

        // Group assignment only
        var groupCollection = new Collection { Name = "Group", OrganizationId = organization.Id };
        await collectionRepository.CreateAsync(groupCollection,
            groups:
            [
                new CollectionAccessSelection {Id = group.Id, ReadOnly = false, HidePasswords = true, Manage = false}
            ],
            users: []);

        // Both — direct wins
        var bothCollection = new Collection { Name = "Both", OrganizationId = organization.Id };
        await collectionRepository.CreateAsync(bothCollection,
            groups:
            [
                new CollectionAccessSelection {Id = group.Id, ReadOnly = false, HidePasswords = false, Manage = true}
            ],
            users:
            [
                new CollectionAccessSelection {Id = orgUser.Id, ReadOnly = true, HidePasswords = true, Manage = false}
            ]);

        var result = await collectionRepository.GetManyByUserIdAsync(user.Id);

        Assert.Equal(3, result.Count);

        var direct = Assert.Single(result, c => c.Id == directCollection.Id);
        Assert.True(direct.ReadOnly);
        Assert.False(direct.HidePasswords);
        Assert.False(direct.Manage);

        var viaGroup = Assert.Single(result, c => c.Id == groupCollection.Id);
        Assert.False(viaGroup.ReadOnly);
        Assert.True(viaGroup.HidePasswords);
        Assert.False(viaGroup.Manage);

        var both = Assert.Single(result, c => c.Id == bothCollection.Id);
        Assert.True(both.ReadOnly);
        Assert.True(both.HidePasswords);
        Assert.False(both.Manage);
    }

    /// <summary>
    /// Group membership alone does not grant access — the group must be assigned to the collection. A collection the
    /// user has no path to is not returned.
    /// </summary>
    [Theory, DatabaseData]
    public async Task GetManyByUserIdAsync_UnassignedCollections_AreNotReturned(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IGroupRepository groupRepository,
        ICollectionRepository collectionRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var user = await userRepository.CreateTestUserAsync();
        var orgUser = await organizationUserRepository.CreateTestOrganizationUserAsync(organization, user);

        // The user is in a group, but that group has no access to the collections below
        var group = await groupRepository.CreateTestGroupAsync(organization);
        await groupRepository.UpdateUsersAsync(group.Id, [orgUser.Id], DateTime.UtcNow);

        // No access relationships at all
        await collectionRepository.CreateAsync(
            new Collection { Name = "Unassigned", OrganizationId = organization.Id },
            groups: [], users: []);

        // Assigned to another group the user does not belong to
        var otherGroup = await groupRepository.CreateTestGroupAsync(organization, "other");
        await collectionRepository.CreateAsync(
            new Collection { Name = "Other group", OrganizationId = organization.Id },
            groups: [new CollectionAccessSelection { Id = otherGroup.Id, Manage = true }],
            users: []);

        // Assigned directly to a different member
        var otherUser = await userRepository.CreateTestUserAsync();
        var otherOrgUser = await organizationUserRepository.CreateTestOrganizationUserAsync(organization, otherUser);
        await collectionRepository.CreateAsync(
            new Collection { Name = "Other user", OrganizationId = organization.Id },
            groups: [],
            users: [new CollectionAccessSelection { Id = otherOrgUser.Id, Manage = true }]);

        var result = await collectionRepository.GetManyByUserIdAsync(user.Id);

        Assert.Empty(result);
    }

    /// <summary>
    /// Only confirmed members see collections. Accepted and revoked members get nothing, even when they hold both a
    /// direct and a group assignment.
    /// </summary>
    [Theory, DatabaseData]
    public async Task GetManyByUserIdAsync_MembershipNotConfirmed_ReturnsNoCollections(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IGroupRepository groupRepository,
        ICollectionRepository collectionRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();

        var acceptedUser = await userRepository.CreateTestUserAsync();
        var acceptedOrgUser =
            await organizationUserRepository.CreateAcceptedTestOrganizationUserAsync(organization, acceptedUser);

        var revokedUser = await userRepository.CreateTestUserAsync();
        var revokedOrgUser =
            await organizationUserRepository.CreateRevokedTestOrganizationUserAsync(organization, revokedUser);

        var group = await groupRepository.CreateTestGroupAsync(organization);
        await groupRepository.UpdateUsersAsync(group.Id, [acceptedOrgUser.Id, revokedOrgUser.Id], DateTime.UtcNow);

        await collectionRepository.CreateAsync(
            new Collection { Name = "Restricted", OrganizationId = organization.Id },
            groups: [new CollectionAccessSelection { Id = group.Id, Manage = true }],
            users:
            [
                new CollectionAccessSelection {Id = acceptedOrgUser.Id, Manage = true},
                new CollectionAccessSelection {Id = revokedOrgUser.Id, Manage = true}
            ]);

        Assert.Empty(await collectionRepository.GetManyByUserIdAsync(acceptedUser.Id));
        Assert.Empty(await collectionRepository.GetManyByUserIdAsync(revokedUser.Id));
    }

    /// <summary>
    /// Collections in a disabled organization are not returned, whether access is direct or group-derived.
    /// </summary>
    [Theory, DatabaseData]
    public async Task GetManyByUserIdAsync_DisabledOrganization_ReturnsNoCollections(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IGroupRepository groupRepository,
        ICollectionRepository collectionRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var user = await userRepository.CreateTestUserAsync();
        var orgUser = await organizationUserRepository.CreateTestOrganizationUserAsync(organization, user);

        var group = await groupRepository.CreateTestGroupAsync(organization);
        await groupRepository.UpdateUsersAsync(group.Id, [orgUser.Id], DateTime.UtcNow);

        await collectionRepository.CreateAsync(
            new Collection { Name = "Direct", OrganizationId = organization.Id },
            groups: [],
            users: [new CollectionAccessSelection { Id = orgUser.Id, Manage = true }]);

        await collectionRepository.CreateAsync(
            new Collection { Name = "Group", OrganizationId = organization.Id },
            groups: [new CollectionAccessSelection { Id = group.Id, Manage = true }],
            users: []);

        organization.Enabled = false;
        await organizationRepository.ReplaceAsync(organization);

        var result = await collectionRepository.GetManyByUserIdAsync(user.Id);

        Assert.Empty(result);
    }

    /// <summary>
    /// Collections are returned across every organization the user is a confirmed member of, with each
    /// organization's permission source resolved independently.
    /// </summary>
    [Theory, DatabaseData]
    public async Task GetManyByUserIdAsync_MultipleOrganizations_ReturnsCollectionsFromEach(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IGroupRepository groupRepository,
        ICollectionRepository collectionRepository)
    {
        var user = await userRepository.CreateTestUserAsync();

        var organization1 = await organizationRepository.CreateTestOrganizationAsync(identifier: "org1");
        var orgUser1 = await organizationUserRepository.CreateTestOrganizationUserAsync(organization1, user);
        var collection1 = new Collection { Name = "Org 1 direct", OrganizationId = organization1.Id };
        await collectionRepository.CreateAsync(collection1,
            groups: [],
            users: [new CollectionAccessSelection { Id = orgUser1.Id, ReadOnly = true }]);

        var organization2 = await organizationRepository.CreateTestOrganizationAsync(identifier: "org2");
        var orgUser2 = await organizationUserRepository.CreateTestOrganizationUserAsync(organization2, user);
        var group2 = await groupRepository.CreateTestGroupAsync(organization2);
        await groupRepository.UpdateUsersAsync(group2.Id, [orgUser2.Id], DateTime.UtcNow);
        var collection2 = new Collection { Name = "Org 2 group", OrganizationId = organization2.Id };
        await collectionRepository.CreateAsync(collection2,
            groups: [new CollectionAccessSelection { Id = group2.Id, Manage = true }],
            users: []);

        var result = await collectionRepository.GetManyByUserIdAsync(user.Id);

        Assert.Equal(2, result.Count);

        var org1Collection = Assert.Single(result, c => c.Id == collection1.Id);
        Assert.Equal(organization1.Id, org1Collection.OrganizationId);
        Assert.True(org1Collection.ReadOnly);
        Assert.False(org1Collection.Manage);

        var org2Collection = Assert.Single(result, c => c.Id == collection2.Id);
        Assert.Equal(organization2.Id, org2Collection.OrganizationId);
        Assert.False(org2Collection.ReadOnly);
        Assert.True(org2Collection.Manage);
    }
}
