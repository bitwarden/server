using AutoMapper;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.RustSDK;
using Bit.Seeder.Data.Enums;
using Bit.Seeder.Factories;
using LinqToDB.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using EfOrganization = Bit.Infrastructure.EntityFramework.AdminConsole.Models.Organization;
using EfOrganizationUser = Bit.Infrastructure.EntityFramework.Models.OrganizationUser;
using EfUser = Bit.Infrastructure.EntityFramework.Models.User;

namespace Bit.Seeder.Recipes;

public class OrganizationWithUsersRecipe(
    DatabaseContext db,
    IMapper mapper,
    RustSdkService sdkService,
    IPasswordHasher<User> passwordHasher)
{
    public static Guid SeedFromServices(
        IServiceProvider services,
        string name,
        string domain,
        int users,
        int ciphers = 0,
        OrganizationUserStatusType usersStatus = OrganizationUserStatusType.Confirmed,
        OrgStructureModel? structureModel = null)
    {
        var db = services.GetRequiredService<DatabaseContext>();
        var mapper = services.GetRequiredService<IMapper>();
        var sdkService = services.GetRequiredService<RustSdkService>();
        var passwordHasher = services.GetRequiredService<IPasswordHasher<User>>();

        var recipe = new OrganizationWithUsersRecipe(db, mapper, sdkService, passwordHasher);
        return recipe.Seed(name, domain, users, ciphers, usersStatus, structureModel);
    }

    /// <summary>
    /// Seeds an organization with users and optionally encrypted ciphers.
    /// Users can log in with their email and password "asdfasdfasdf".
    /// Organization and user keys are generated dynamically for each run.
    /// </summary>
    /// <param name="structureModel">Optional org structure for realistic collection names (e.g., Traditional departments, Spotify tribes).</param>
    public Guid Seed(
        string name,
        string domain,
        int users,
        int ciphers = 0,
        OrganizationUserStatusType usersStatus = OrganizationUserStatusType.Confirmed,
        OrgStructureModel? structureModel = null)
    {
        var seats = Math.Max(users + 1, 1000);
        var orgKeys = sdkService.GenerateOrganizationKeys();

        var organization = OrganizationSeeder.CreateEnterprise(name, domain, seats);
        organization.PublicKey = orgKeys.PublicKey;
        organization.PrivateKey = orgKeys.PrivateKey;

        var ownerUser = UserSeeder.CreateUserWithSdkKeys($"owner@{domain}", sdkService, passwordHasher);

        var ownerOrgKey = sdkService.GenerateUserOrganizationKey(ownerUser.PublicKey!, orgKeys.Key);
        var ownerOrgUser = organization.CreateOrganizationUserWithKey(
            ownerUser, OrganizationUserType.Owner, OrganizationUserStatusType.Confirmed, ownerOrgKey);

        var memberUsers = new List<User>();
        var memberOrgUsers = new List<OrganizationUser>();
        for (var i = 0; i < users; i++)
        {
            var memberUser = UserSeeder.CreateUserWithSdkKeys($"user{i}@{domain}", sdkService, passwordHasher);
            memberUsers.Add(memberUser);

            var memberOrgKey = (usersStatus == OrganizationUserStatusType.Confirmed ||
                                usersStatus == OrganizationUserStatusType.Revoked)
                ? sdkService.GenerateUserOrganizationKey(memberUser.PublicKey!, orgKeys.Key)
                : null;

            memberOrgUsers.Add(organization.CreateOrganizationUserWithKey(
                memberUser, OrganizationUserType.User, usersStatus, memberOrgKey));
        }

        db.Add(mapper.Map<EfOrganization>(organization));
        db.Add(mapper.Map<EfUser>(ownerUser));
        db.Add(mapper.Map<EfOrganizationUser>(ownerOrgUser));

        // BulkCopy for performance with large user counts
        var efMemberUsers = memberUsers.Select(u => mapper.Map<EfUser>(u)).ToList();
        var efMemberOrgUsers = memberOrgUsers.Select(ou => mapper.Map<EfOrganizationUser>(ou)).ToList();
        db.BulkCopy(efMemberUsers);
        db.BulkCopy(efMemberOrgUsers);

        db.SaveChanges();

        // Create collections - either from org structure or single default
        var allOrgUserIds = memberOrgUsers
            .Where(ou => ou.Status == OrganizationUserStatusType.Confirmed)
            .Select(ou => ou.Id)
            .Prepend(ownerOrgUser.Id)
            .ToList();

        List<Guid> collectionIds;
        if (structureModel.HasValue)
        {
            var collectionsRecipe = new CollectionsRecipe(db, sdkService);
            collectionIds = collectionsRecipe.AddFromStructure(
                organization.Id,
                orgKeys.Key,
                structureModel.Value,
                allOrgUserIds);
        }
        else
        {
            var defaultCollection = new CollectionSeeder(sdkService)
                .CreateCollection(organization.Id, orgKeys.Key, "Default Collection");
            db.BulkCopy(new[] { defaultCollection });

            var collectionUsers = allOrgUserIds
                .Select((id, i) => CollectionSeeder.CreateCollectionUser(defaultCollection.Id, id, manage: i == 0))
                .ToList();
            db.BulkCopy(collectionUsers);

            collectionIds = [defaultCollection.Id];
        }

        if (ciphers > 0)
        {
            var cipherRecipe = new CiphersRecipe(db, sdkService);
            cipherRecipe.AddLoginCiphersToOrganization(
                organization.Id,
                orgKeys.Key,
                collectionIds,
                count: ciphers);
        }

        return organization.Id;
    }
}
