using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Repositories.CollectionRepository;

public class CollectionRepositoryReplaceTests
{
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

        // Clean up data
        await userRepository.DeleteAsync(user);
        await organizationRepository.DeleteAsync(organization);
        await groupRepository.DeleteManyAsync([group1.Id, group2.Id, group3.Id]);
        await organizationUserRepository.DeleteManyAsync([orgUser1.Id, orgUser2.Id, orgUser3.Id]);
    }

}
