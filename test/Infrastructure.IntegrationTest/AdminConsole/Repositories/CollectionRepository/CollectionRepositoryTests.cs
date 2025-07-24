﻿using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Repositories.CollectionRepository;

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

        // Create a default user collection (should be excluded from admin console results)
        var defaultCollection = new Collection
        {
            Name = "My Items Collection",
            OrganizationId = organization.Id,
            Type = CollectionType.DefaultUserCollection
        };

        await collectionRepository.CreateAsync(defaultCollection, null, users: new[]
        {
            new CollectionAccessSelection()
            {
                Id = orgUser.Id, HidePasswords = false, ReadOnly = false, Manage = true
            }
        });

        var collections = await collectionRepository.GetManyByOrganizationIdWithPermissionsAsync(organization.Id, user.Id, true);

        Assert.NotNull(collections);

        // Should return only 3 collections (excluding the default user collection)
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

    /// <summary>
    /// Test to ensure collections are properly retrieved by organization
    /// </summary>
    [DatabaseTheory, DatabaseData]
    public async Task GetManyByOrganizationIdAsync_Success(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IOrganizationUserRepository organizationUserRepository)
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

        var collection1 = new Collection { Name = "Collection 1", OrganizationId = organization.Id, };
        await collectionRepository.CreateAsync(collection1, null, null);

        var collection2 = new Collection { Name = "Collection 2", OrganizationId = organization.Id, };
        await collectionRepository.CreateAsync(collection2, null, null);

        var collection3 = new Collection { Name = "Collection 3", OrganizationId = organization.Id, };
        await collectionRepository.CreateAsync(collection3, null, null);

        // Create a default user collection
        var defaultCollection = new Collection
        {
            Name = "My Items",
            OrganizationId = organization.Id,
            Type = CollectionType.DefaultUserCollection
        };
        await collectionRepository.CreateAsync(defaultCollection, null, null);

        var collections = await collectionRepository.GetManyByOrganizationIdAsync(organization.Id);

        Assert.NotNull(collections);
        Assert.Equal(4, collections.Count);
        Assert.All(collections, c => Assert.Equal(organization.Id, c.OrganizationId));

        Assert.Contains(collections, c => c.Name == "Collection 1");
        Assert.Contains(collections, c => c.Name == "Collection 2");
        Assert.Contains(collections, c => c.Name == "Collection 3");
        Assert.Contains(collections, c => c.Name == "My Items");
    }

    /// <summary>
    /// Test to ensure organization properly retrieves shared collections
    /// </summary>
    [DatabaseTheory, DatabaseData]
    public async Task GetManySharedCollectionsByOrganizationIdAsync_Success(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IOrganizationUserRepository organizationUserRepository)
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

        var collection1 = new Collection { Name = "Collection 1", OrganizationId = organization.Id, };
        await collectionRepository.CreateAsync(collection1, null, null);

        var collection2 = new Collection { Name = "Collection 2", OrganizationId = organization.Id, };
        await collectionRepository.CreateAsync(collection2, null, null);

        var collection3 = new Collection { Name = "Collection 3", OrganizationId = organization.Id, };
        await collectionRepository.CreateAsync(collection3, null, null);

        // Create a default user collection (should not be returned by this method)
        var defaultCollection = new Collection
        {
            Name = "My Items",
            OrganizationId = organization.Id,
            Type = CollectionType.DefaultUserCollection
        };
        await collectionRepository.CreateAsync(defaultCollection, null, null);

        var collections = await collectionRepository.GetManySharedCollectionsByOrganizationIdAsync(organization.Id);

        Assert.NotNull(collections);
        Assert.Equal(3, collections.Count); // Should only return the 3 shared collections, excluding the default user collection
        Assert.All(collections, c => Assert.Equal(organization.Id, c.OrganizationId));

        Assert.Contains(collections, c => c.Name == "Collection 1");
        Assert.Contains(collections, c => c.Name == "Collection 2");
        Assert.Contains(collections, c => c.Name == "Collection 3");
        Assert.DoesNotContain(collections, c => c.Name == "My Items");
    }
}
