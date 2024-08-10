using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Repositories;

public class CollectionRepositoryTests
{
    /// <summary>
    /// Test to ensure that access relationships are retrieved when requested
    /// </summary>
    [DatabaseTheory, DatabaseData]
    public async Task GetByIdWithPermissionsAsync_WithRelationships_Success(IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IOrganizationUserRepository organizationUserRepository, IGroupRepository groupRepository)
    {
        var user = await userRepository.CreateAsync(new User
        {
            Name = "Test User",
            Email = $"test+{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var organization = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Org",
            PlanType = PlanType.EnterpriseAnnually,
            Plan = "Test Plan",
            BillingEmail = "billing@email.com"
        });

        var orgUser = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user.Id,
            Status = OrganizationUserStatusType.Confirmed,
        });

        var group = await groupRepository.CreateAsync(new Group
        {
            Name = "Test Group",
            OrganizationId = organization.Id,
        });

        var collection = new Collection { Name = "Test Collection", OrganizationId = organization.Id, };

        await collectionRepository.CreateAsync(collection, groups: new[]
        {
            new CollectionAccessSelection
            {
                Id = group.Id, HidePasswords = false, ReadOnly = true, Manage = false
            }
        }, users: new[]
        {
            new CollectionAccessSelection()
            {
                Id = orgUser.Id, HidePasswords = false, ReadOnly = false, Manage = true
            }
        });

        var collectionWithPermissions = await collectionRepository.GetByIdWithPermissionsAsync(collection.Id, user.Id, true);

        Assert.NotNull(collectionWithPermissions);
        Assert.Equal(1, collectionWithPermissions.Users?.Count());
        Assert.Equal(1, collectionWithPermissions.Groups?.Count());
        Assert.True(collectionWithPermissions.Assigned);
        Assert.True(collectionWithPermissions.Manage);
        Assert.False(collectionWithPermissions.ReadOnly);
        Assert.False(collectionWithPermissions.HidePasswords);
    }

    /// <summary>
    /// Test to ensure that a user's explicitly assigned permissions replaces any group permissions
    /// that user may belong to
    /// </summary>
    [DatabaseTheory, DatabaseData]
    public async Task GetByIdWithPermissionsAsync_UserOverrideGroup_Success(IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IOrganizationUserRepository organizationUserRepository, IGroupRepository groupRepository)
    {
        var user = await userRepository.CreateAsync(new User
        {
            Name = "Test User",
            Email = $"test+{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var organization = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Org",
            PlanType = PlanType.EnterpriseAnnually,
            Plan = "Test Plan",
            BillingEmail = "billing@email.com"
        });

        var orgUser = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user.Id,
            Status = OrganizationUserStatusType.Confirmed,
        });

        var group = await groupRepository.CreateAsync(new Group
        {
            Name = "Test Group",
            OrganizationId = organization.Id,
        });

        // Assign the test user to the test group
        await groupRepository.UpdateUsersAsync(group.Id, new[] { orgUser.Id });

        var collection = new Collection { Name = "Test Collection", OrganizationId = organization.Id, };

        await collectionRepository.CreateAsync(collection, groups: new[]
        {
            new CollectionAccessSelection
            {
                Id = group.Id, HidePasswords = false, ReadOnly = false, Manage = true // Group is Manage
            }
        }, users: new[]
        {
            new CollectionAccessSelection()
            {
                Id = orgUser.Id, HidePasswords = false, ReadOnly = true, Manage = false // User is given ReadOnly (should override group)
            }
        });

        var collectionWithPermissions = await collectionRepository.GetByIdWithPermissionsAsync(collection.Id, user.Id, true);

        Assert.NotNull(collectionWithPermissions);
        Assert.Equal(1, collectionWithPermissions.Users?.Count());
        Assert.Equal(1, collectionWithPermissions.Groups?.Count());
        Assert.True(collectionWithPermissions.Assigned);
        Assert.False(collectionWithPermissions.Manage);
        Assert.True(collectionWithPermissions.ReadOnly);
        Assert.False(collectionWithPermissions.HidePasswords);
    }

    /// <summary>
    /// Test to ensure that the returned permissions are the most permissive combination of group permissions when
    /// multiple groups are assigned to the same collection with different permissions
    /// </summary>
    [DatabaseTheory, DatabaseData]
    public async Task GetByIdWithPermissionsAsync_CombineGroupPermissions_Success(IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IOrganizationUserRepository organizationUserRepository, IGroupRepository groupRepository)
    {
        var user = await userRepository.CreateAsync(new User
        {
            Name = "Test User",
            Email = $"test+{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var organization = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Org",
            PlanType = PlanType.EnterpriseAnnually,
            Plan = "Test Plan",
            BillingEmail = "billing@email.com"
        });

        var orgUser = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user.Id,
            Status = OrganizationUserStatusType.Confirmed,
        });

        var group = await groupRepository.CreateAsync(new Group
        {
            Name = "Test Group",
            OrganizationId = organization.Id,
        });

        var group2 = await groupRepository.CreateAsync(new Group
        {
            Name = "Test Group 2",
            OrganizationId = organization.Id,
        });

        // Assign the test user to the test groups
        await groupRepository.UpdateUsersAsync(group.Id, new[] { orgUser.Id });
        await groupRepository.UpdateUsersAsync(group2.Id, new[] { orgUser.Id });

        var collection = new Collection { Name = "Test Collection", OrganizationId = organization.Id, };

        await collectionRepository.CreateAsync(collection, groups: new[]
        {
            new CollectionAccessSelection
            {
                Id = group.Id, HidePasswords = false, ReadOnly = true, Manage = false // Group 1 is ReadOnly
            },
            new CollectionAccessSelection
            {
                Id = group2.Id, HidePasswords = false, ReadOnly = false, Manage = true // Group 2 is Manage
            }
        }, users: new List<CollectionAccessSelection>()); // No explicit user permissions for this test

        var collectionWithPermissions = await collectionRepository.GetByIdWithPermissionsAsync(collection.Id, user.Id, true);

        Assert.NotNull(collectionWithPermissions);
        Assert.Equal(2, collectionWithPermissions.Groups?.Count());
        Assert.True(collectionWithPermissions.Assigned);

        // Since Group2 is Manage the user should have Manage
        Assert.True(collectionWithPermissions.Manage);

        // Similarly, ReadOnly and HidePassword should be false
        Assert.False(collectionWithPermissions.ReadOnly);
        Assert.False(collectionWithPermissions.HidePasswords);
    }

    /// <summary>
    /// Test to ensure the basic usage works as expected
    /// </summary>
    [DatabaseTheory, DatabaseData]
    public async Task GetManyByOrganizationIdWithPermissionsAsync_Success(IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IOrganizationUserRepository organizationUserRepository, IGroupRepository groupRepository)
    {
        var user = await userRepository.CreateAsync(new User
        {
            Name = "Test User",
            Email = $"test+{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var organization = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Org",
            PlanType = PlanType.EnterpriseAnnually,
            Plan = "Test Plan",
            BillingEmail = "billing@email.com"
        });

        var orgUser = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user.Id,
            Status = OrganizationUserStatusType.Confirmed,
        });

        var group = await groupRepository.CreateAsync(new Group
        {
            Name = "Test Group",
            OrganizationId = organization.Id,
        });

        var collection1 = new Collection { Name = "Collection 1", OrganizationId = organization.Id, };

        await collectionRepository.CreateAsync(collection1, groups: new[]
        {
            new CollectionAccessSelection
            {
                Id = group.Id, HidePasswords = false, ReadOnly = true, Manage = false
            }
        }, users: new[]
        {
            new CollectionAccessSelection()
            {
                Id = orgUser.Id, HidePasswords = false, ReadOnly = false, Manage = true
            }
        });

        var collection2 = new Collection { Name = "Collection 2", OrganizationId = organization.Id, };

        await collectionRepository.CreateAsync(collection2, null, users: new[]
        {
            new CollectionAccessSelection()
            {
                Id = orgUser.Id, HidePasswords = false, ReadOnly = true, Manage = false
            }
        });

        var collection3 = new Collection { Name = "Collection 3", OrganizationId = organization.Id, };

        await collectionRepository.CreateAsync(collection3, groups: new[]
        {
            new CollectionAccessSelection()
            {
                Id = group.Id, HidePasswords = false, ReadOnly = false, Manage = true
            }
        }, null);

        var collections = await collectionRepository.GetManyByOrganizationIdWithPermissionsAsync(organization.Id, user.Id, true);

        Assert.NotNull(collections);

        collections = collections.OrderBy(c => c.Name).ToList();

        Assert.Collection(collections, c1 =>
        {
            Assert.NotNull(c1);
            Assert.Equal(1, c1.Users?.Count());
            Assert.Equal(1, c1.Groups?.Count());
            Assert.True(c1.Assigned);
            Assert.True(c1.Manage);
            Assert.False(c1.ReadOnly);
            Assert.False(c1.HidePasswords);
            Assert.False(c1.Unmanaged);
        }, c2 =>
        {
            Assert.NotNull(c2);
            Assert.Equal(1, c2.Users?.Count());
            Assert.Equal(0, c2.Groups?.Count());
            Assert.True(c2.Assigned);
            Assert.False(c2.Manage);
            Assert.True(c2.ReadOnly);
            Assert.False(c2.HidePasswords);
            Assert.True(c2.Unmanaged);
        }, c3 =>
        {
            Assert.NotNull(c3);
            Assert.Equal(0, c3.Users?.Count());
            Assert.Equal(1, c3.Groups?.Count());
            Assert.False(c3.Assigned);
            Assert.False(c3.Manage);
            Assert.False(c3.ReadOnly);
            Assert.False(c3.HidePasswords);
            Assert.False(c3.Unmanaged);
        });
    }

    /// <summary>
    /// Test to ensure collections assigned to multiple groups do not duplicate in the results
    /// </summary>
    [DatabaseTheory, DatabaseData]
    public async Task GetManyByOrganizationIdWithPermissionsAsync_GroupBy_Success(IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IOrganizationUserRepository organizationUserRepository, IGroupRepository groupRepository)
    {
        var user = await userRepository.CreateAsync(new User
        {
            Name = "Test User",
            Email = $"test+{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var organization = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Org",
            PlanType = PlanType.EnterpriseAnnually,
            Plan = "Test Plan",
            BillingEmail = "billing@email.com"
        });

        var orgUser = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user.Id,
            Status = OrganizationUserStatusType.Confirmed,
        });

        var group = await groupRepository.CreateAsync(new Group
        {
            Name = "Test Group",
            OrganizationId = organization.Id,
        });

        var group2 = await groupRepository.CreateAsync(new Group
        {
            Name = "Test Group 2",
            OrganizationId = organization.Id,
        });

        // Assign the test user to the test groups
        await groupRepository.UpdateUsersAsync(group.Id, new[] { orgUser.Id });
        await groupRepository.UpdateUsersAsync(group2.Id, new[] { orgUser.Id });

        var collection1 = new Collection { Name = "Collection 1", OrganizationId = organization.Id, };

        await collectionRepository.CreateAsync(collection1, groups: new[]
        {
            new CollectionAccessSelection
            {
                Id = group.Id, HidePasswords = false, ReadOnly = true, Manage = false
            },
        }, users: new[]
        {
            new CollectionAccessSelection()
            {
                Id = orgUser.Id, HidePasswords = false, ReadOnly = false, Manage = true
            }
        });

        var collection2 = new Collection { Name = "Collection 2", OrganizationId = organization.Id, };

        await collectionRepository.CreateAsync(collection2, null, users: new[]
        {
            new CollectionAccessSelection()
            {
                Id = orgUser.Id, HidePasswords = false, ReadOnly = true, Manage = false
            }
        });

        var collection3 = new Collection { Name = "Collection 3", OrganizationId = organization.Id, };

        await collectionRepository.CreateAsync(collection3, groups: new[]
        {
            new CollectionAccessSelection()
            {
                Id = group.Id, HidePasswords = false, ReadOnly = false, Manage = true
            },
            new CollectionAccessSelection()
            {
                Id = group2.Id, HidePasswords = false, ReadOnly = true, Manage = false
            }
        }, null);

        var collections = await collectionRepository.GetManyByOrganizationIdWithPermissionsAsync(organization.Id, user.Id, true);

        Assert.NotNull(collections);

        Assert.Equal(3, collections.Count);

        collections = collections.OrderBy(c => c.Name).ToList();

        Assert.Collection(collections, c1 =>
        {
            Assert.NotNull(c1);
            Assert.Equal(1, c1.Users?.Count());
            Assert.Equal(1, c1.Groups?.Count());
            Assert.True(c1.Assigned);
            Assert.True(c1.Manage);
            Assert.False(c1.ReadOnly);
            Assert.False(c1.HidePasswords);
            Assert.False(c1.Unmanaged);
        }, c2 =>
        {
            Assert.NotNull(c2);
            Assert.Equal(1, c2.Users?.Count());
            Assert.Equal(0, c2.Groups?.Count());
            Assert.True(c2.Assigned);
            Assert.False(c2.Manage);
            Assert.True(c2.ReadOnly);
            Assert.False(c2.HidePasswords);
            Assert.True(c2.Unmanaged);
        }, c3 =>
        {
            Assert.NotNull(c3);
            Assert.Equal(0, c3.Users?.Count());
            Assert.Equal(2, c3.Groups?.Count());
            Assert.True(c3.Assigned); // User is a member of both Groups
            Assert.True(c3.Manage); // Group 2 is Manage
            Assert.False(c3.ReadOnly);
            Assert.False(c3.HidePasswords);
            Assert.False(c3.Unmanaged);
        });
    }

    [DatabaseTheory, DatabaseData]
    public async Task ReplaceAsync_Works(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IGroupRepository groupRepository,
        ICollectionRepository collectionRepository)
    {
        var user = await userRepository.CreateAsync(new User
        {
            Name = "Test User",
            Email = $"test+{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var organization = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Org",
            PlanType = PlanType.EnterpriseAnnually,
            Plan = "Test Plan",
            BillingEmail = "billing@email.com"
        });

        var orgUser1 = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user.Id,
            Status = OrganizationUserStatusType.Confirmed,
        });

        var orgUser2 = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user.Id,
            Status = OrganizationUserStatusType.Confirmed,
        });

        var orgUser3 = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user.Id,
            Status = OrganizationUserStatusType.Confirmed,
        });

        var group1 = await groupRepository.CreateAsync(new Group
        {
            Name = "Test Group #1",
            OrganizationId = organization.Id,
        });

        var group2 = await groupRepository.CreateAsync(new Group
        {
            Name = "Test Group #2",
            OrganizationId = organization.Id,
        });

        var group3 = await groupRepository.CreateAsync(new Group
        {
            Name = "Test Group #3",
            OrganizationId = organization.Id,
        });

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

        collection.Name = "Updated Collection Name";

        await collectionRepository.ReplaceAsync(collection,
            [
                // Should delete group1
                new CollectionAccessSelection { Id = group2.Id, Manage = true, HidePasswords = true, ReadOnly = false, },
                // Should add group3
                new CollectionAccessSelection { Id = group3.Id, Manage = false, HidePasswords = false, ReadOnly = true, },
            ],
            [
                // Should delete orgUser1
                new CollectionAccessSelection { Id = orgUser2.Id, Manage = false, HidePasswords = false, ReadOnly = true },
                // Should add orgUser3
                new CollectionAccessSelection { Id = orgUser3.Id, Manage = true, HidePasswords = false, ReadOnly = true },
            ]
        );

        // Assert it
        var info = await collectionRepository.GetByIdWithPermissionsAsync(collection.Id, user.Id, true);

        Assert.NotNull(info);

        Assert.Equal("Updated Collection Name", info.Name);

        var groups = info.Groups.ToArray();

        Assert.Equal(2, groups.Length);

        var actualGroup2 = Assert.Single(groups.Where(g => g.Id == group2.Id));

        Assert.True(actualGroup2.Manage);
        Assert.True(actualGroup2.HidePasswords);
        Assert.False(actualGroup2.ReadOnly);

        var actualGroup3 = Assert.Single(groups.Where(g => g.Id == group3.Id));

        Assert.False(actualGroup3.Manage);
        Assert.False(actualGroup3.HidePasswords);
        Assert.True(actualGroup3.ReadOnly);

        var users = info.Users.ToArray();

        Assert.Equal(2, users.Length);

        var actualOrgUser2 = Assert.Single(users.Where(u => u.Id == orgUser2.Id));

        Assert.False(actualOrgUser2.Manage);
        Assert.False(actualOrgUser2.HidePasswords);
        Assert.True(actualOrgUser2.ReadOnly);

        var actualOrgUser3 = Assert.Single(users.Where(u => u.Id == orgUser3.Id));

        Assert.True(actualOrgUser3.Manage);
        Assert.False(actualOrgUser3.HidePasswords);
        Assert.True(actualOrgUser3.ReadOnly);
    }
}
