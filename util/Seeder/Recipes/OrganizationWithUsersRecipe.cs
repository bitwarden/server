using AutoMapper;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.RustSDK;
using Bit.Seeder.Factories;
using Bit.Seeder.Services;
using LinqToDB.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using EfOrganization = Bit.Infrastructure.EntityFramework.AdminConsole.Models.Organization;
using EfOrganizationUser = Bit.Infrastructure.EntityFramework.Models.OrganizationUser;
using EfUser = Bit.Infrastructure.EntityFramework.Models.User;

namespace Bit.Seeder.Recipes;

public class OrganizationWithUsersRecipe(
    DatabaseContext db,
    IMapper mapper,
    IPasswordHasher<User> passwordHasher,
    IManglerService manglerService)
{
    public Guid Seed(string name, string domain, int users, OrganizationUserStatusType usersStatus = OrganizationUserStatusType.Confirmed)
    {
        var seats = Math.Max(users + 1, 1000);

        // Generate organization keys
        var orgKeys = RustSdkService.GenerateOrganizationKeys();
        var organization = OrganizationSeeder.Create(
            name, domain, seats, manglerService, orgKeys.PublicKey, orgKeys.PrivateKey);

        // Create owner with SDK-generated keys
        var (ownerUser, _) = UserSeeder.Create($"owner@{domain}", passwordHasher, manglerService);
        var ownerOrgKey = RustSdkService.GenerateUserOrganizationKey(ownerUser.PublicKey!, orgKeys.Key);
        var ownerOrgUser = organization.CreateOrganizationUserWithKey(
            ownerUser, OrganizationUserType.Owner, OrganizationUserStatusType.Confirmed, ownerOrgKey);

        var additionalUsers = new List<User>();
        var additionalOrgUsers = new List<OrganizationUser>();
        for (var i = 0; i < users; i++)
        {
            var (additionalUser, _) = UserSeeder.Create($"user{i}@{domain}", passwordHasher, manglerService);
            additionalUsers.Add(additionalUser);

            // Generate org key for confirmed/revoked users
            var shouldHaveKey = usersStatus == OrganizationUserStatusType.Confirmed
                || usersStatus == OrganizationUserStatusType.Revoked;
            var userOrgKey = shouldHaveKey
                ? RustSdkService.GenerateUserOrganizationKey(additionalUser.PublicKey!, orgKeys.Key)
                : null;

            additionalOrgUsers.Add(organization.CreateOrganizationUserWithKey(
                additionalUser, OrganizationUserType.User, usersStatus, userOrgKey));
        }

        // Map Core entities to EF entities before adding to DbContext
        db.Add(mapper.Map<EfOrganization>(organization));
        db.Add(mapper.Map<EfUser>(ownerUser));
        db.Add(mapper.Map<EfOrganizationUser>(ownerOrgUser));

        // Map and BulkCopy additional users
        var efAdditionalUsers = additionalUsers.Select(u => mapper.Map<EfUser>(u)).ToList();
        var efAdditionalOrgUsers = additionalOrgUsers.Select(ou => mapper.Map<EfOrganizationUser>(ou)).ToList();

        db.BulkCopy(efAdditionalUsers);
        db.BulkCopy(efAdditionalOrgUsers);

        db.SaveChanges();

        return organization.Id;
    }
}
