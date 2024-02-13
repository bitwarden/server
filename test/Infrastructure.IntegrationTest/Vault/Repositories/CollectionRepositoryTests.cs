using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Repositories;

public class CollectionRepositoryTests
{
    [DatabaseTheory, DatabaseData]
    public async Task GetByIdWithPermissionsAsync_Works(IUserRepository userRepository,
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

    [DatabaseTheory, DatabaseData]
    public async Task GetManyByOrganizationIdWithPermissionsAsync(IUserRepository userRepository,
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
        }, c2 =>
        {
            Assert.NotNull(c2);
            Assert.Equal(1, c2.Users?.Count());
            Assert.Equal(0, c2.Groups?.Count());
            Assert.True(c2.Assigned);
            Assert.False(c2.Manage);
            Assert.True(c2.ReadOnly);
            Assert.False(c2.HidePasswords);
        }, c3 =>
        {
            Assert.NotNull(c3);
            Assert.Equal(0, c3.Users?.Count());
            Assert.Equal(1, c3.Groups?.Count());
            Assert.False(c3.Assigned);
            Assert.False(c3.Manage);
            Assert.False(c3.ReadOnly);
            Assert.False(c3.HidePasswords);
        });
    }
}
