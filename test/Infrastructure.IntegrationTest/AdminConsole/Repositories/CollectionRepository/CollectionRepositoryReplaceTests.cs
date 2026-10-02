using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Entities;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Pam.Entities;
using Bit.Pam.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Repositories.CollectionRepository;

public class CollectionRepositoryReplaceTests
{
    [DatabaseTheory, DatabaseData]
    public async Task ReplaceAsync_WithAccess_Works(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IGroupRepository groupRepository,
        ICollectionRepository collectionRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();

        var user1 = await userRepository.CreateTestUserAsync();
        var orgUser1 = await organizationUserRepository.CreateTestOrganizationUserAsync(organization, user1);

        var user2 = await userRepository.CreateTestUserAsync();
        var orgUser2 = await organizationUserRepository.CreateTestOrganizationUserAsync(organization, user2);

        var user3 = await userRepository.CreateTestUserAsync();
        var orgUser3 = await organizationUserRepository.CreateTestOrganizationUserAsync(organization, user3);

        var group1 = await groupRepository.CreateTestGroupAsync(organization);
        var group2 = await groupRepository.CreateTestGroupAsync(organization);
        var group3 = await groupRepository.CreateTestGroupAsync(organization);

        var collection = new Collection
        {
            Name = "Test Collection Name",
            OrganizationId = organization.Id,
        };

        await collectionRepository.CreateAsync(collection,
            [
                new CollectionAccessSelection { Id = group1.Id, Manage = true, HidePasswords = true, ReadOnly = false, },
                new CollectionAccessSelection { Id = group2.Id, Manage = false, HidePasswords = false, ReadOnly = true, },
            ],
            [
                new CollectionAccessSelection { Id = orgUser1.Id, Manage = true, HidePasswords = false, ReadOnly = true },
                new CollectionAccessSelection { Id = orgUser2.Id, Manage = false, HidePasswords = true, ReadOnly = false },
            ]
        );

        // Act
        collection.Name = "Updated Collection Name";
        collection.RevisionDate = DateTime.UtcNow;

        await collectionRepository.ReplaceAsync(collection,
            [
                // Delete group1
                // Update group2:
                new CollectionAccessSelection { Id = group2.Id, Manage = true, HidePasswords = true, ReadOnly = false, },
                // Add group3:
                new CollectionAccessSelection { Id = group3.Id, Manage = false, HidePasswords = false, ReadOnly = true, },
            ],
            [
                // Delete orgUser1
                // Update orgUser2:
                new CollectionAccessSelection { Id = orgUser2.Id, Manage = false, HidePasswords = false, ReadOnly = true },
                // Add orgUser3:
                new CollectionAccessSelection { Id = orgUser3.Id, Manage = true, HidePasswords = false, ReadOnly = true },
            ]
        );

        // Assert
        var (actualCollection, actualAccess) = await collectionRepository.GetByIdWithAccessAsync(collection.Id);

        Assert.NotNull(actualCollection);
        Assert.Equal("Updated Collection Name", actualCollection.Name);
        Assert.Equal(collection.RevisionDate, actualCollection.RevisionDate, TimeSpan.FromMilliseconds(10));

        var groups = actualAccess.Groups.ToArray();
        Assert.Equal(2, groups.Length);
        Assert.Single(groups, g => g.Id == group2.Id && g.Manage && g.HidePasswords && !g.ReadOnly);
        Assert.Single(groups, g => g.Id == group3.Id && !g.Manage && !g.HidePasswords && g.ReadOnly);

        var users = actualAccess.Users.ToArray();

        Assert.Equal(2, users.Length);
        Assert.Single(users, u => u.Id == orgUser2.Id && !u.Manage && !u.HidePasswords && u.ReadOnly);
        Assert.Single(users, u => u.Id == orgUser3.Id && u.Manage && !u.HidePasswords && u.ReadOnly);
    }

    /// <remarks>
    /// Makes sure that the sproc handles empty sets.
    /// </remarks>
    [DatabaseTheory, DatabaseData]
    public async Task ReplaceAsync_WithNoAccess_Works(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IGroupRepository groupRepository,
        ICollectionRepository collectionRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();

        var user = await userRepository.CreateTestUserAsync();
        var orgUser = await organizationUserRepository.CreateTestOrganizationUserAsync(organization, user);

        var group = await groupRepository.CreateTestGroupAsync(organization);

        var collection = new Collection
        {
            Name = "Test Collection Name",
            OrganizationId = organization.Id,
        };

        await collectionRepository.CreateAsync(collection,
        [
                new CollectionAccessSelection { Id = group.Id, Manage = true, HidePasswords = false, ReadOnly = true },
        ],
        [
                new CollectionAccessSelection { Id = orgUser.Id, Manage = true, HidePasswords = false, ReadOnly = true },
        ]);

        // Act
        collection.Name = "Updated Collection Name";

        await collectionRepository.ReplaceAsync(collection, [], []);

        // Assert
        var (actualCollection, actualAccess) = await collectionRepository.GetByIdWithAccessAsync(collection.Id);

        Assert.NotNull(actualCollection);
        Assert.Equal("Updated Collection Name", actualCollection.Name);

        Assert.Empty(actualAccess.Groups);
        Assert.Empty(actualAccess.Users);
    }

    [Theory, DatabaseData]
    public async Task ReplaceAsync_WhenNotPassingGroupsOrUsers_DoesNotDeleteAccess(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IGroupRepository groupRepository,
        ICollectionRepository collectionRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();

        var user1 = await userRepository.CreateTestUserAsync();
        var orgUser1 = await organizationUserRepository.CreateTestOrganizationUserAsync(organization, user1);

        var user2 = await userRepository.CreateTestUserAsync();
        var orgUser2 = await organizationUserRepository.CreateTestOrganizationUserAsync(organization, user2);

        var group1 = await groupRepository.CreateTestGroupAsync(organization);
        var group2 = await groupRepository.CreateTestGroupAsync(organization);

        var collection = new Collection
        {
            Name = "Test Collection Name",
            OrganizationId = organization.Id,
        };

        await collectionRepository.CreateAsync(collection,
            [
                new CollectionAccessSelection { Id = group1.Id, Manage = true, HidePasswords = true, ReadOnly = false, },
                new CollectionAccessSelection { Id = group2.Id, Manage = false, HidePasswords = false, ReadOnly = true, },
            ],
            [
                new CollectionAccessSelection { Id = orgUser1.Id, Manage = true, HidePasswords = false, ReadOnly = true },
                new CollectionAccessSelection { Id = orgUser2.Id, Manage = false, HidePasswords = true, ReadOnly = false },
            ]
        );

        // Act
        collection.Name = "Updated Collection Name";

        await collectionRepository.ReplaceAsync(collection, null, null);

        // Assert
        var (actualCollection, actualAccess) = await collectionRepository.GetByIdWithAccessAsync(collection.Id);

        Assert.NotNull(actualCollection);
        Assert.Equal("Updated Collection Name", actualCollection.Name);

        var groups = actualAccess.Groups.ToArray();
        Assert.Equal(2, groups.Length);
        Assert.Single(groups, g => g.Id == group1.Id && g.Manage && g.HidePasswords && !g.ReadOnly);
        Assert.Single(groups, g => g.Id == group2.Id && !g.Manage && !g.HidePasswords && g.ReadOnly);

        var users = actualAccess.Users.ToArray();

        Assert.Equal(2, users.Length);
        Assert.Single(users, u => u.Id == orgUser1.Id && u.Manage && !u.HidePasswords && u.ReadOnly);
        Assert.Single(users, u => u.Id == orgUser2.Id && !u.Manage && u.HidePasswords && !u.ReadOnly);
    }

    /// <summary>
    /// <see cref="ICollectionRepository.ReplaceAsync"/> is the standard collection-edit path and knows nothing about
    /// PAM, so it must leave <see cref="Collection.AccessRuleId"/> exactly as it found it. The MSSQL implementation
    /// picks one of four stored procedures depending on which of <c>groups</c>/<c>users</c> are supplied, and all four
    /// funnel into <c>Collection_Update</c> — so every branch of that matrix is exercised here.
    /// </summary>
    [DatabaseTheory, DatabaseData]
    public async Task ReplaceAsync_WithGovernedCollection_PreservesAccessRuleId(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IGroupRepository groupRepository,
        ICollectionRepository collectionRepository,
        IAccessRuleRepository accessRuleRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var rule = await accessRuleRepository.CreateAsync(new AccessRule
        {
            OrganizationId = organization.Id,
            Name = $"Replace {Guid.NewGuid()}",
            Conditions = """{"kind":"human_approval"}""",
        });

        var user = await userRepository.CreateTestUserAsync();
        var orgUser = await organizationUserRepository.CreateTestOrganizationUserAsync(organization, user);
        var group = await groupRepository.CreateTestGroupAsync(organization);

        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        await collectionRepository.SetAccessRuleAssociationsAsync(
            organization.Id, rule.Id, [collection.Id], []);

        var governed = await collectionRepository.GetByIdAsync(collection.Id);
        Assert.NotNull(governed);
        Assert.Equal(rule.Id, governed.AccessRuleId);

        CollectionAccessSelection[] groups = [new() { Id = group.Id, Manage = true }];
        CollectionAccessSelection[] users = [new() { Id = orgUser.Id, Manage = true }];

        // Act & Assert: each branch of ReplaceAsync's group/user matrix must preserve the association.
        foreach (var (withGroups, withUsers, branch) in new (CollectionAccessSelection[]?, CollectionAccessSelection[]?, string)[]
        {
            (null, null, "no groups or users"),
            (groups, null, "groups only"),
            (null, users, "users only"),
            (groups, users, "groups and users"),
        })
        {
            governed.Name = $"Updated for {branch}";
            await collectionRepository.ReplaceAsync(governed, withGroups, withUsers);

            var actual = await collectionRepository.GetByIdAsync(collection.Id);
            Assert.NotNull(actual);
            Assert.Equal(rule.Id, actual.AccessRuleId);

            // Guard against a vacuous pass: the update must actually have been applied.
            Assert.Equal($"Updated for {branch}", actual.Name);
        }
    }

    /// <summary>
    /// <see cref="Collection.AccessRuleId"/> has a single writer,
    /// <see cref="ICollectionRepository.SetAccessRuleAssociationsAsync"/>. Every other write path ignores the
    /// property, which is what makes an accidental erasure structurally impossible rather than merely fixed — so a
    /// caller that mutates it and submits a whole-entity update must not move the stored value in either direction.
    /// </summary>
    [DatabaseTheory, DatabaseData]
    public async Task ReplaceAsync_WithMutatedAccessRuleId_IgnoresIt(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IAccessRuleRepository accessRuleRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var rule = await accessRuleRepository.CreateAsync(new AccessRule
        {
            OrganizationId = organization.Id,
            Name = $"Ignored {Guid.NewGuid()}",
            Conditions = """{"kind":"human_approval"}""",
        });

        var ungoverned = await collectionRepository.CreateTestCollectionAsync(organization);
        var governed = await collectionRepository.CreateTestCollectionAsync(organization);
        await collectionRepository.SetAccessRuleAssociationsAsync(
            organization.Id, rule.Id, [governed.Id], []);

        // Act: try to forge an association on the ungoverned collection...
        var forged = await collectionRepository.GetByIdAsync(ungoverned.Id);
        Assert.NotNull(forged);
        forged.AccessRuleId = rule.Id;
        await collectionRepository.ReplaceAsync(forged, null, null);

        // ...and to erase the real one on the governed collection.
        var erased = await collectionRepository.GetByIdAsync(governed.Id);
        Assert.NotNull(erased);
        erased.AccessRuleId = null;
        await collectionRepository.ReplaceAsync(erased, null, null);

        // Assert: neither write reached the database.
        var actualUngoverned = await collectionRepository.GetByIdAsync(ungoverned.Id);
        Assert.NotNull(actualUngoverned);
        Assert.Null(actualUngoverned.AccessRuleId);

        var actualGoverned = await collectionRepository.GetByIdAsync(governed.Id);
        Assert.NotNull(actualGoverned);
        Assert.Equal(rule.Id, actualGoverned.AccessRuleId);
    }

    /// <summary>
    /// The same single-writer rule on the create path: a brand-new collection is always ungoverned, so an
    /// <see cref="Collection.AccessRuleId"/> set before <see cref="ICollectionRepository.CreateAsync"/> is ignored.
    /// </summary>
    [DatabaseTheory, DatabaseData]
    public async Task CreateAsync_WithAccessRuleId_IgnoresIt(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IAccessRuleRepository accessRuleRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var rule = await accessRuleRepository.CreateAsync(new AccessRule
        {
            OrganizationId = organization.Id,
            Name = $"Create {Guid.NewGuid()}",
            Conditions = """{"kind":"human_approval"}""",
        });

        var collection = new Collection
        {
            Name = "Forged At Creation",
            OrganizationId = organization.Id,
            AccessRuleId = rule.Id,
        };

        // Act
        await collectionRepository.CreateAsync(collection, null, null);

        // Assert
        var actual = await collectionRepository.GetByIdAsync(collection.Id);
        Assert.NotNull(actual);
        Assert.Null(actual.AccessRuleId);
    }
}
