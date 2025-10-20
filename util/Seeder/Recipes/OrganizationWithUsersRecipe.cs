using Bit.Infrastructure.EntityFramework.Models;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Seeder.Factories;
using LinqToDB.EntityFrameworkCore;

namespace Bit.Seeder.Recipes;

public class OrganizationWithUsersRecipe(DatabaseContext db)
{
    public Guid Seed(string name, int users, string domain, string? label = null)
    {
        var labeledName = label is null ? name : $"{name} [SEED:{label}]";
        var organization = OrganizationSeeder.CreateEnterprise(labeledName, domain, users);
        var adminEmail = label is null ? $"admin@{domain}" : $"seed-{label}-admin@{domain}";
        var user = UserSeeder.CreateUser(adminEmail);
        var orgUser = organization.CreateOrganizationUser(user);

        var additionalUsers = new List<User>();
        var additionalOrgUsers = new List<OrganizationUser>();
        for (var i = 0; i < users; i++)
        {
            var email = label is null ? $"user{i}@{domain}" : $"seed-{label}-{i}@{domain}";
            var additionalUser = UserSeeder.CreateUser(email);
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
}
