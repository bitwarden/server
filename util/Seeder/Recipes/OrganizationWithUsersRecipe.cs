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
        var user = UserSeeder.CreateUser($"admin@{domain}");
        var orgUser = organization.CreateOrganizationUser(user);

        var additionalUsers = new List<User>();
        var additionalOrgUsers = new List<OrganizationUser>();
        for (var i = 0; i < users; i++)
        {
            var additionalUser = UserSeeder.CreateUser($"user{i}@{domain}");
            additionalUsers.Add(additionalUser);
            additionalOrgUsers.Add(organization.CreateOrganizationUser(additionalUser));
        }

        // Create collections for the organization
        var collections = CreateCollections(organization.Id);

        // Create groups for the organization  
        var groups = CreateGroups(organization.Id);

        db.Add(organization);
        db.Add(user);
        db.Add(orgUser);

        // Add collections and groups
        db.AddRange(collections);
        db.AddRange(groups);

        db.SaveChanges();

        // Use LinqToDB's BulkCopy for significant better performance
        db.BulkCopy(additionalUsers);
        db.BulkCopy(additionalOrgUsers);

        // Create associations between users, groups, and collections
        // Assign to all additional users (not the admin)
        var (collectionUsers, groupUsers) = CreateUserAssociations(additionalOrgUsers, collections, groups);

        // Bulk insert associations
        db.BulkCopy(new BulkCopyOptions { TableName = "CollectionUser" }, collectionUsers);
        db.BulkCopy(new BulkCopyOptions { TableName = "GroupUser" }, groupUsers);

        return organization.Id;
    }

    private List<Collection> CreateCollections(Guid organizationId)
    {
        var collectionNames = new[]
        {
            "Engineering",
            "Marketing",
            "Sales",
            "HR",
            "Finance",
            "Legal",
            "Operations",
            "Customer Support",
            "Product",
            "Design"
        };

        return collectionNames.Select(name => new Collection
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = name,
            CreationDate = DateTime.UtcNow,
            RevisionDate = DateTime.UtcNow
        }).ToList();
    }

    private List<Group> CreateGroups(Guid organizationId)
    {
        var groupNames = new[]
        {
            "Administrators",
            "Team Leads",
            "Senior Engineers",
            "Junior Engineers",
            "Marketing Team",
            "Sales Team",
            "HR Team",
            "Finance Team"
        };

        return groupNames.Select(name => new Group
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = name
        }).ToList();
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
