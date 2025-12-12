using Bit.Core.Enums;
using Bit.Infrastructure.EntityFramework.Models;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Seeder.Factories;
using LinqToDB.EntityFrameworkCore;

namespace Bit.Seeder.Recipes;

public class OrganizationWithUsersRecipe(DatabaseContext db)
{
    public Guid Seed(string name, string domain, int users, OrganizationUserStatusType usersStatus = OrganizationUserStatusType.Confirmed, string? label = null)
    {
        var labeledName = label is null ? name : $"{name} [SEED:{label}]";
        var seats = Math.Max(users + 1, 1000);
        var organization = OrganizationSeeder.CreateEnterprise(labeledName, domain, seats);
        var ownerUser = UserSeeder.CreateUser($"owner@{domain}");
        var ownerOrgUser = organization.CreateOrganizationUser(ownerUser, OrganizationUserType.Owner, OrganizationUserStatusType.Confirmed);

        var additionalUsers = new List<User>();
        var additionalOrgUsers = new List<OrganizationUser>();
        for (var i = 0; i < users; i++)
        {
            var email = label is null ? $"user{i}@{domain}" : $"seed-{label}-{i}@{domain}";
            var additionalUser = UserSeeder.CreateUser(email);
            additionalUsers.Add(additionalUser);
            additionalOrgUsers.Add(organization.CreateOrganizationUser(additionalUser, OrganizationUserType.User, usersStatus));
        }

        db.Add(organization);
        db.Add(ownerUser);
        db.Add(ownerOrgUser);

        db.SaveChanges();

        // Use LinqToDB's BulkCopy for significant better performance
        db.BulkCopy(additionalUsers);
        db.BulkCopy(additionalOrgUsers);

        return organization.Id;
    }
}
