using Bit.Infrastructure.EntityFramework.AdminConsole.Models;
using Bit.Infrastructure.EntityFramework.Models;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Seeder.Factories;
using LinqToDB.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

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

        db.Add(organization);
        db.Add(user);
        db.Add(orgUser);

        db.SaveChanges();

        // Use LinqToDB's BulkCopy for significant better performance
        db.BulkCopy(additionalUsers);
        db.BulkCopy(additionalOrgUsers);

        return organization.Id;
    }

    public void Destroy(Guid organizationId)
    {
        var organization = db.Organizations.Include(o => o.OrganizationUsers)
            .ThenInclude(ou => ou.User).FirstOrDefault(p => p.Id == organizationId);
        if (organization == null)
        {
            throw new Exception($"Organization with ID {organizationId} not found.");
        }
        var users = organization.OrganizationUsers.Select(u => u.User);

        db.RemoveRange(users);
        db.Remove(organization);
    }
}
