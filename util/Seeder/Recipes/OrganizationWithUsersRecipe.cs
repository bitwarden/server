using Bit.Infrastructure.EntityFramework.Models;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Seeder.Factories;
using Bit.Seeder.Services;
using LinqToDB.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

namespace Bit.Seeder.Recipes;

public class OrganizationWithUsersRecipe(DatabaseContext db, ISeederCryptoService cryptoService, IPasswordHasher<User> passwordHasher)
{
    public Guid Seed(string name, int users, string domain, string defaultPassword = "Test123!@#")
    {
        // TODO: When Rust SDK is available, these crypto operations will use RustSeederCryptoService

        // Create organization with proper crypto keys
        var organizationSeeder = new OrganizationSeeder(cryptoService);
        var organization = organizationSeeder.CreateEnterpriseWithCrypto(name, domain, users);

        // Create admin user with proper crypto
        var userSeeder = new UserSeeder(cryptoService);
        var user = CreateUserWithPasswordHasher(userSeeder, $"admin@{domain}", defaultPassword, passwordHasher);
        var orgUser = organization.CreateOrganizationUser(user);

        var additionalUsers = new List<User>();
        var additionalOrgUsers = new List<OrganizationUser>();
        for (var i = 0; i < users; i++)
        {
            var additionalUser = CreateUserWithPasswordHasher(userSeeder, $"user{i}@{domain}", defaultPassword, passwordHasher);
            additionalUsers.Add(additionalUser);
            additionalOrgUsers.Add(organization.CreateOrganizationUser(additionalUser));
        }

        db.Add(organization);
        db.Add(user);
        db.Add(orgUser);

        db.SaveChanges();

        // Use LinqToDB's BulkCopy for significant better performance
        db.BulkCopy(additionalUsers);
        db.BulkCopy(additionalOrgUsers);

        return organization.Id;
    }

    private User CreateUserWithPasswordHasher(UserSeeder userSeeder, string email, string password, IPasswordHasher<User> passwordHasher)
    {
        // The UserSeeder now properly handles double-hashing internally
        return userSeeder.CreateUser(email, password);
    }
}
