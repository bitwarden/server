using Bit.Core.Enums;
using Bit.Infrastructure.EntityFramework.Models;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.RustSDK;
using Bit.Seeder.Factories;
using Bogus;
using LinqToDB.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Collection = Bit.Infrastructure.EntityFramework.Models.Collection;

namespace Bit.Seeder.Recipes;

public class OrganizationWithUsersRecipe(DatabaseContext db, IPasswordHasher<Bit.Core.Entities.User> passwordHasher)
{
    public Guid Seed(string name, string domain, int users, int collections = 0, int ciphersPerCollection = 0, OrganizationUserStatusType usersStatus = OrganizationUserStatusType.Confirmed)
    {
        var rustSdkService = RustSdkServiceFactory.CreateSingleton();

        // Generate organization keys once and use them throughout
        var orgKeys = rustSdkService.GenerateOrganizationKeys();

        var seats = Math.Max(users + 1, 1000);
        var organization = OrganizationSeeder.CreateEnterprise(name, domain, seats, orgKeys);

        var (ownerUser, _) = UserSeeder.CreateSdkUser(passwordHasher, $"owner@{domain}");
        var ownerOrgUser = organization.CreateOrganizationUser(
            ownerUser,
            OrganizationUserType.Owner,
            OrganizationUserStatusType.Confirmed,
            orgKeys.Key,  // Pass the organization's symmetric key
            rustSdkService
        );

        var additionalUsers = new List<User>();
        var additionalOrgUsers = new List<OrganizationUser>();
        for (var i = 0; i < users; i++)
        {
            var (additionalUser, _) = UserSeeder.CreateSdkUser(passwordHasher, $"user{i}@{domain}", logToConsole: false);
            additionalUsers.Add(additionalUser);
            additionalOrgUsers.Add(organization.CreateOrganizationUser(
                additionalUser,
                OrganizationUserType.User,
                usersStatus,
                orgKeys.Key,  // Pass the organization's symmetric key
                rustSdkService
            ));
        }

        // Create collections with Bogus-generated names
        var organizationCollections = new List<Collection>();
        if (collections > 0)
        {
            var faker = new Faker();
            var collectionNames = new HashSet<string>();

            // Generate unique collection names
            while (collectionNames.Count < collections)
            {
                // Mix different name types for variety
                var nameType = faker.Random.Number(0, 3);
                var generatedName = nameType switch
                {
                    0 => faker.Commerce.Department(),
                    1 => $"{faker.Commerce.ProductAdjective()} {faker.Commerce.Product()}",
                    2 => $"{faker.Hacker.Verb()} {faker.Hacker.Noun()}",
                    _ => $"{faker.Company.CompanySuffix()} {faker.Commerce.ProductName()}"
                };

                collectionNames.Add(generatedName);
            }

            foreach (var collectionName in collectionNames)
            {
                var encryptedName = rustSdkService.EncryptString(collectionName, orgKeys.Key);
                organizationCollections.Add(new Collection
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = organization.Id,
                    Name = encryptedName,
                    Type = Core.Enums.CollectionType.SharedCollection
                });
            }
        }

        db.Add(organization);
        db.Add(ownerUser);
        db.Add(ownerOrgUser);

        db.SaveChanges();

        // Use LinqToDB's BulkCopy for significant better performance
        db.BulkCopy(additionalUsers);
        db.BulkCopy(additionalOrgUsers);

        if (organizationCollections.Count > 0)
        {
            db.BulkCopy(organizationCollections);
            Console.WriteLine($"Created {organizationCollections.Count} collections");

            // Create CollectionUser relationships for owner with full permissions
            var ownerCollectionUsers = organizationCollections.Select(collection => new Core.Entities.CollectionUser
            {
                CollectionId = collection.Id,
                OrganizationUserId = ownerOrgUser.Id,
                ReadOnly = false,       // Owner can edit
                HidePasswords = false,  // Owner can view passwords
                Manage = true           // Owner can manage
            }).ToList();

            db.BulkCopy(ownerCollectionUsers);
        }

        // Generate and insert ciphers if requested
        if (ciphersPerCollection > 0 && organizationCollections.Count > 0)
        {
            var totalCiphers = collections * ciphersPerCollection;
            var ciphers = CipherSeeder.CreateCiphers(
                totalCiphers,
                organization.Id,
                orgKeys.Key,
                rustSdkService
            );

            // Build CollectionCipher relationships (sequential distribution)
            var collectionCiphers = new List<Core.Entities.CollectionCipher>();
            for (var i = 0; i < ciphers.Count; i++)
            {
                var collectionIndex = i % organizationCollections.Count;
                collectionCiphers.Add(new Core.Entities.CollectionCipher
                {
                    CollectionId = organizationCollections[collectionIndex].Id,
                    CipherId = ciphers[i].Id
                });
            }

            // Bulk insert
            db.BulkCopy(ciphers);
            db.BulkCopy(collectionCiphers);

            Console.WriteLine($"Created {ciphers.Count} ciphers across {organizationCollections.Count} collections");
        }

        return organization.Id;
    }
}
