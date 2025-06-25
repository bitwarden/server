using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Utilities;
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

        // Create sample custom permissions for Custom user types
        var customPermissions = new Permissions
        {
            AccessEventLogs = true,
            AccessImportExport = false,
            AccessReports = true,
            CreateNewCollections = true,
            EditAnyCollection = false,
            DeleteAnyCollection = false,
            ManageGroups = false,
            ManagePolicies = false,
            ManageSso = false,
            ManageUsers = false,
            ManageResetPassword = false,
            ManageScim = false
        };

        var customPermissionsJson = CoreHelpers.ClassToJsonData(customPermissions);

        // Define claimed and unclaimed domains for testing
        var claimedDomains = new[] { "example1.com", "example2.com", "example3.com" };
        var unclaimedDomains = new[] { "example4.com", "example5.com", "example6.com" };

        for (var i = 0; i < users; i++)
        {
            // 80% users have claimed domains, 20% have unclaimed domains
            var useClaimedDomain = i < (users * 0.8);
            string userDomain;

            if (useClaimedDomain)
            {
                userDomain = claimedDomains[i % claimedDomains.Length];
            }
            else
            {
                userDomain = unclaimedDomains[i % unclaimedDomains.Length];
            }

            var additionalUser = UserSeeder.CreateUser($"user{i}@{userDomain}");
            additionalUsers.Add(additionalUser);

            // Create OrganizationUser with mixed types to test the optimization
            var additionalOrgUser = organization.CreateOrganizationUser(additionalUser);

            // Set permissions for ALL users to test the optimization
            additionalOrgUser.Permissions = customPermissionsJson;

            // Distribute user types to test the optimization:
            // - 50% Custom users (with serialized permissions) - these should have permissions processed after optimization
            // - 50% mixed other types (Admin/User/Owner with serialized permissions) - these should be skipped after optimization
            var userTypeDistribution = i % 2;

            if (userTypeDistribution == 0) // 50% Custom users
            {
                additionalOrgUser.Type = OrganizationUserType.Custom;
            }
            else // 50% other types
            {
                var otherTypeDistribution = i % 6;
                if (otherTypeDistribution < 2) // ~17% Admin users
                {
                    additionalOrgUser.Type = OrganizationUserType.Admin;
                }
                else if (otherTypeDistribution < 5) // ~25% User type
                {
                    additionalOrgUser.Type = OrganizationUserType.User;
                }
                else // ~8% Owner type
                {
                    additionalOrgUser.Type = OrganizationUserType.Owner;
                }
            }

            additionalOrgUsers.Add(additionalOrgUser);
        }

        // Create organization domains - claimed domains will be verified, unclaimed ones won't
        var organizationDomains = new List<OrganizationDomain>();

        foreach (var claimedDomain in claimedDomains)
        {
            var orgDomain = new OrganizationDomain
            {
                Id = Guid.NewGuid(),
                OrganizationId = organization.Id,
                DomainName = claimedDomain,
                Txt = $"bw={CoreHelpers.RandomString(44)}",
                CreationDate = DateTime.UtcNow
            };
            orgDomain.SetNextRunDate(12);
            orgDomain.SetVerifiedDate(); // Mark as claimed/verified
            orgDomain.SetJobRunCount();
            organizationDomains.Add(orgDomain);
        }

        // Create collections for the organization
        var collections = CreateCollections(organization.Id);

        // Create groups for the organization  
        var groups = CreateGroups(organization.Id);

        db.Add(organization);
        db.Add(user);
        db.Add(orgUser);

        // Add organization domains, collections and groups
        db.AddRange(organizationDomains);
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
