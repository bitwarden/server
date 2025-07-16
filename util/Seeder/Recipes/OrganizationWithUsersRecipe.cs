using Bit.Infrastructure.EntityFramework.Models;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Seeder.Factories;
using LinqToDB.Data;
using LinqToDB.EntityFrameworkCore;

namespace Bit.Seeder.Recipes;

public class OrganizationWithUsersRecipe(DatabaseContext db)
{
    public Guid Seed(string name, int users, string domain)
    {
        var organization = OrganizationSeeder.CreateEnterprise(name, domain, users);
        var adminUser = UserSeeder.CreateUser($"admin@{domain}");
        var adminOrgUser = organization.CreateOrganizationUser(adminUser);

        // Create users with domain distribution (80% claimed, 20% unclaimed)
        var (additionalUsers, additionalOrgUsers) = organization.CreateUsersWithDomainDistribution(users);

        // Create organization domains for claimed domains
        var organizationDomains = organization.CreateOrganizationDomains();

        // Create collections and groups for the organization
        var collections = organization.CreateCollections();
        var groups = organization.CreateGroups();

        // Use BulkCopy for everything - much better performance
        db.BulkCopy(new[] { organization });
        db.BulkCopy(new[] { adminUser });
        db.BulkCopy(additionalUsers);
        db.BulkCopy(new[] { adminOrgUser });
        db.BulkCopy(additionalOrgUsers);
        db.BulkCopy(organizationDomains);
        db.BulkCopy(collections);
        db.BulkCopy(groups);

        // Create and bulk insert user associations
        var (collectionUsers, groupUsers) = CreateUserAssociations(additionalOrgUsers, collections, groups);
        db.BulkCopy(new BulkCopyOptions { TableName = "CollectionUser" }, collectionUsers);
        db.BulkCopy(new BulkCopyOptions { TableName = "GroupUser" }, groupUsers);

        return organization.Id;
    }

    private (List<CollectionUser>, List<GroupUser>) CreateUserAssociations(
        List<OrganizationUser> orgUsers,
        List<Collection> collections,
        List<Group> groups)
    {
        var random = new Random(42); // Fixed seed for reproducible results
        var collectionUsers = new List<CollectionUser>();
        var groupUsers = new List<GroupUser>();

        foreach (var orgUser in orgUsers)
        {
            // Assign each user to a random group
            var userGroup = groups[random.Next(0, groups.Count)];
            groupUsers.Add(new GroupUser
            {
                GroupId = userGroup.Id,
                OrganizationUserId = orgUser.Id
            });

            // Give users access to 2-4 random collections
            var numCollections = random.Next(2, 5);
            var assignedCollections = collections
                .OrderBy(x => random.Next())
                .Take(numCollections);

            foreach (var collection in assignedCollections)
            {
                var isReadOnly = random.Next(0, 3) == 0; // 33% chance of read-only
                var canManage = !isReadOnly && random.Next(0, 4) == 0; // 25% chance of manage if not read-only

                collectionUsers.Add(new CollectionUser
                {
                    CollectionId = collection.Id,
                    OrganizationUserId = orgUser.Id,
                    ReadOnly = isReadOnly,
                    HidePasswords = random.Next(0, 5) == 0, // 20% chance
                    Manage = canManage
                });
            }
        }

        return (collectionUsers, groupUsers);
    }
}
