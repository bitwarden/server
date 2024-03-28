using AutoMapper;
using AutoMapper.QueryableExtensions;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Organization = Bit.Infrastructure.EntityFramework.AdminConsole.Models.Organization;

namespace Bit.Infrastructure.EntityFramework.Repositories;

public class OrganizationRepository : Repository<Core.AdminConsole.Entities.Organization, Organization, Guid>, IOrganizationRepository
{
    private readonly ILogger<OrganizationRepository> _logger;

    public OrganizationRepository(
        IServiceScopeFactory serviceScopeFactory,
        IMapper mapper,
        ILogger<OrganizationRepository> logger)
        : base(serviceScopeFactory, mapper, context => context.Organizations)
    {
        _logger = logger;
    }

    public async Task<Core.AdminConsole.Entities.Organization> GetByIdentifierAsync(string identifier)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var organization = await GetDbSet(dbContext).Where(e => e.Identifier == identifier)
                .FirstOrDefaultAsync();
            return organization;
        }
    }

    public async Task<ICollection<Core.AdminConsole.Entities.Organization>> GetManyByEnabledAsync()
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var organizations = await GetDbSet(dbContext).Where(e => e.Enabled).ToListAsync();
            return Mapper.Map<List<Core.AdminConsole.Entities.Organization>>(organizations);
        }
    }

    public async Task<ICollection<Core.AdminConsole.Entities.Organization>> GetManyByUserIdAsync(Guid userId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var organizations = await GetDbSet(dbContext)
                .Select(e => e.OrganizationUsers
                    .Where(ou => ou.UserId == userId)
                    .Select(ou => ou.Organization))
                .ToListAsync();
            return Mapper.Map<List<Core.AdminConsole.Entities.Organization>>(organizations);
        }
    }

    public async Task<ICollection<Core.AdminConsole.Entities.Organization>> SearchAsync(string name, string userEmail,
        bool? paid, int skip, int take)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var organizations = await GetDbSet(dbContext)
                .Where(e => name == null || e.Name.Contains(name))
                .Where(e => userEmail == null || e.OrganizationUsers.Any(u => u.Email == userEmail))
                .Where(e => paid == null ||
                        (paid == true && !string.IsNullOrWhiteSpace(e.GatewaySubscriptionId)) ||
                        (paid == false && e.GatewaySubscriptionId == null))
                .OrderBy(e => e.CreationDate)
                .Skip(skip).Take(take)
                .ToListAsync();
            return Mapper.Map<List<Core.AdminConsole.Entities.Organization>>(organizations);
        }
    }

    public async Task<ICollection<OrganizationAbility>> GetManyAbilitiesAsync()
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            return await GetDbSet(dbContext)
            .Select(e => new OrganizationAbility
            {
                Enabled = e.Enabled,
                Id = e.Id,
                Use2fa = e.Use2fa,
                UseEvents = e.UseEvents,
                UsersGetPremium = e.UsersGetPremium,
                Using2fa = e.Use2fa && e.TwoFactorProviders != null,
                UseSso = e.UseSso,
                UseKeyConnector = e.UseKeyConnector,
                UseResetPassword = e.UseResetPassword,
                UseScim = e.UseScim,
                UseCustomPermissions = e.UseCustomPermissions,
                UsePolicies = e.UsePolicies,
                LimitCollectionCreationDeletion = e.LimitCollectionCreationDeletion,
                AllowAdminAccessToAllCollectionItems = e.AllowAdminAccessToAllCollectionItems,
                FlexibleCollections = e.FlexibleCollections
            }).ToListAsync();
        }
    }

    public async Task<ICollection<Core.AdminConsole.Entities.Organization>> SearchUnassignedToProviderAsync(string name, string ownerEmail, int skip, int take)
    {
        using var scope = ServiceScopeFactory.CreateScope();

        var dbContext = GetDatabaseContext(scope);

        var query =
            from o in dbContext.Organizations
            where
                ((o.PlanType >= PlanType.TeamsMonthly2019 && o.PlanType <= PlanType.EnterpriseAnnually2019) ||
                 (o.PlanType >= PlanType.TeamsMonthly2020 && o.PlanType <= PlanType.EnterpriseAnnually)) &&
                !dbContext.ProviderOrganizations.Any(po => po.OrganizationId == o.Id) &&
                (string.IsNullOrWhiteSpace(name) || EF.Functions.Like(o.Name, $"%{name}%"))
            select o;

        if (string.IsNullOrWhiteSpace(ownerEmail))
        {
            return await query.OrderByDescending(o => o.CreationDate)
                .Skip(skip)
                .Take(take)
                .ToArrayAsync();
        }

        if (dbContext.Database.IsNpgsql())
        {
            query = from o in query
                    join ou in dbContext.OrganizationUsers
                        on o.Id equals ou.OrganizationId
                    join u in dbContext.Users
                        on ou.UserId equals u.Id
                    where ou.Type == OrganizationUserType.Owner && EF.Functions.ILike(EF.Functions.Collate(u.Email, "default"), $"{ownerEmail}%")
                    select o;
        }
        else
        {
            query = from o in query
                    join ou in dbContext.OrganizationUsers
                        on o.Id equals ou.OrganizationId
                    join u in dbContext.Users
                        on ou.UserId equals u.Id
                    where ou.Type == OrganizationUserType.Owner && EF.Functions.Like(u.Email, $"{ownerEmail}%")
                    select o;
        }

        return await query.OrderByDescending(o => o.CreationDate).Skip(skip).Take(take).ToArrayAsync();
    }

    public async Task UpdateStorageAsync(Guid id)
    {
        await OrganizationUpdateStorage(id);
    }

    public override async Task DeleteAsync(Core.AdminConsole.Entities.Organization organization)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            await dbContext.UserBumpAccountRevisionDateByOrganizationIdAsync(organization.Id);
            var deleteCiphersTransaction = await dbContext.Database.BeginTransactionAsync();
            await dbContext.Ciphers.Where(c => c.UserId == null && c.OrganizationId == organization.Id)
                .ExecuteDeleteAsync();
            await deleteCiphersTransaction.CommitAsync();

            var organizationDeleteTransaction = await dbContext.Database.BeginTransactionAsync();
            await dbContext.AuthRequests.Where(ar => ar.OrganizationId == organization.Id)
                .ExecuteDeleteAsync();
            await dbContext.SsoUsers.Where(su => su.OrganizationId == organization.Id)
                .ExecuteDeleteAsync();
            await dbContext.SsoConfigs.Where(sc => sc.OrganizationId == organization.Id)
                .ExecuteDeleteAsync();
            await dbContext.CollectionUsers.Where(cu => cu.OrganizationUser.OrganizationId == organization.Id)
                .ExecuteDeleteAsync();
            await dbContext.UserProjectAccessPolicy.Where(ap => ap.OrganizationUser.OrganizationId == organization.Id)
                .ExecuteDeleteAsync();
            await dbContext.UserServiceAccountAccessPolicy.Where(ap => ap.OrganizationUser.OrganizationId == organization.Id)
                .ExecuteDeleteAsync();
            await dbContext.UserSecretAccessPolicy.Where(ap => ap.OrganizationUser.OrganizationId == organization.Id)
                .ExecuteDeleteAsync();
            await dbContext.OrganizationUsers.Where(ou => ou.OrganizationId == organization.Id)
                .ExecuteDeleteAsync();
            await dbContext.ProviderOrganizations.Where(po => po.OrganizationId == organization.Id)
                .ExecuteDeleteAsync();

            await dbContext.GroupServiceAccountAccessPolicy.Where(ap => ap.GrantedServiceAccount.OrganizationId == organization.Id)
                .ExecuteDeleteAsync();
            await dbContext.Project.Where(p => p.OrganizationId == organization.Id)
                .ExecuteDeleteAsync();
            await dbContext.Secret.Where(s => s.OrganizationId == organization.Id)
                .ExecuteDeleteAsync();
            await dbContext.ApiKeys.Where(ak => ak.ServiceAccount.OrganizationId == organization.Id)
                .ExecuteDeleteAsync();
            await dbContext.ServiceAccount.Where(sa => sa.OrganizationId == organization.Id)
                .ExecuteDeleteAsync();

            // The below section are 3 SPROCS in SQL Server but are only called by here
            await dbContext.OrganizationApiKeys.Where(oa => oa.OrganizationId == organization.Id)
                .ExecuteDeleteAsync();
            await dbContext.OrganizationConnections.Where(oc => oc.OrganizationId == organization.Id)
                .ExecuteDeleteAsync();
            var sponsoringOrgs = await dbContext.OrganizationSponsorships
                .Where(os => os.SponsoringOrganizationId == organization.Id)
                .ToListAsync();
            sponsoringOrgs.ForEach(os => os.SponsoringOrganizationId = null);
            var sponsoredOrgs = await dbContext.OrganizationSponsorships
                .Where(os => os.SponsoredOrganizationId == organization.Id)
                .ToListAsync();
            sponsoredOrgs.ForEach(os => os.SponsoredOrganizationId = null);

            var orgEntity = await dbContext.FindAsync<Organization>(organization.Id);
            dbContext.Remove(orgEntity);

            await organizationDeleteTransaction.CommitAsync();
            await dbContext.SaveChangesAsync();
        }
    }

    public async Task<Core.AdminConsole.Entities.Organization> GetByLicenseKeyAsync(string licenseKey)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var organization = await GetDbSet(dbContext)
                .FirstOrDefaultAsync(o => o.LicenseKey == licenseKey);

            return organization;
        }
    }

    public async Task<SelfHostedOrganizationDetails> GetSelfHostedOrganizationDetailsById(Guid id)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);

            var selfHostedOrganization = await dbContext.Organizations
                .Where(o => o.Id == id)
                .AsSplitQuery()
                .ProjectTo<SelfHostedOrganizationDetails>(Mapper.ConfigurationProvider)
                .SingleOrDefaultAsync();

            return selfHostedOrganization;
        }
    }

    public async Task<IEnumerable<string>> GetOwnerEmailAddressesById(Guid organizationId)
    {
        _logger.LogInformation("AC-1758: Executing GetOwnerEmailAddressesById (Entity Framework)");

        using var scope = ServiceScopeFactory.CreateScope();

        var dbContext = GetDatabaseContext(scope);

        var query =
            from u in dbContext.Users
            join ou in dbContext.OrganizationUsers on u.Id equals ou.UserId
            where
                ou.OrganizationId == organizationId &&
                ou.Type == OrganizationUserType.Owner &&
                ou.Status == OrganizationUserStatusType.Confirmed
            group u by u.Email
            into grouped
            select grouped.Key;

        return await query.ToListAsync();
    }

    public Task EnableCollectionEnhancements(Guid organizationId)
    {
        using var scope = ServiceScopeFactory.CreateScope();

        var dbContext = GetDatabaseContext(scope);

        var query = "";
        if (dbContext.Database.IsSqlite())
        {
            query = _sqliteQuery;

        }
        else if (dbContext.Database.IsMySql())
        {
            query = _mysqlQuery;
        }
        else if (dbContext.Database.IsNpgsql())
        {
            query = _postgresQuery;
        }
        else
        {
            throw new Exception("Invalid db provider");
        }

        dbContext.Database.ExecuteSqlRaw(query);
        return Task.FromResult(0);
    }

    private readonly string _postgresQuery = $"""
                                              -- Step 1: AccessAll migration for Groups
                                                  -- Create a temporary table to store the groups with AccessAll = true
                                                  CREATE TEMPORARY TABLE IF NOT EXISTS "TempGroupsAccessAll" AS
                                                  SELECT "G"."Id" AS "GroupId",
                                                         "G"."OrganizationId"
                                                  FROM "Group" "G"
                                                  INNER JOIN "Organization" "O" ON "G"."OrganizationId" = "O"."Id"
                                                  WHERE "O"."FlexibleCollections" = false AND "G"."AccessAll" = true;

                                              -- Step 2: AccessAll migration for OrganizationUsers
                                                  -- Create a temporary table to store the OrganizationUsers with AccessAll = true
                                                  CREATE TEMPORARY TABLE IF NOT EXISTS "TempUsersAccessAll" AS
                                                  SELECT "OU"."Id" AS "OrganizationUserId",
                                                         "OU"."OrganizationId"
                                                  FROM "OrganizationUser" "OU"
                                                  INNER JOIN "Organization" "O" ON "OU"."OrganizationId" = "O"."Id"
                                                  WHERE "O"."FlexibleCollections" = false AND "OU"."AccessAll" = true;

                                              -- Step 3: For all OrganizationUsers with Manager role or 'EditAssignedCollections' permission update their existing CollectionUsers rows and insert new rows with Manage = 1
                                              -- and finally update all OrganizationUsers with Manager role to User role
                                                  -- Create a temporary table to store the OrganizationUsers with Manager role or 'EditAssignedCollections' permission
                                                  CREATE TEMPORARY TABLE IF NOT EXISTS "TempUserManagers" AS
                                                  SELECT "OU"."Id" AS "OrganizationUserId",
                                                         CASE WHEN "OU"."Type" = 3 THEN true ELSE false END AS "IsManager"
                                                  FROM "OrganizationUser" "OU"
                                                  INNER JOIN "Organization" "O" ON "OU"."OrganizationId" = "O"."Id"
                                                  WHERE "O"."FlexibleCollections" = false AND
                                                        ("OU"."Type" = 3 OR
                                                         ("OU"."Type" = 4 AND
                                                          "OU"."Permissions" IS NOT NULL AND
                                                          (("OU"."Permissions"::text)::jsonb->>'editAssignedCollections') = 'true'));

                                                  -- Step 1
                                                      -- Update existing rows in CollectionGroups
                                                      UPDATE "CollectionGroups" "CG"
                                                      SET "ReadOnly" = false,
                                                          "HidePasswords" = false,
                                                          "Manage" = false
                                                      FROM "Collection" "C"
                                                      WHERE "CG"."CollectionId" = "C"."Id"
                                                      AND "C"."OrganizationId" IN (SELECT "OrganizationId" FROM "TempGroupsAccessAll");

                                                      -- Insert new rows into CollectionGroups
                                                      INSERT INTO "CollectionGroups" ("CollectionId", "GroupId", "ReadOnly", "HidePasswords", "Manage")
                                                      SELECT "C"."Id", "TG"."GroupId", false, false, false
                                                      FROM "Collection" "C"
                                                      INNER JOIN "TempGroupsAccessAll" "TG" ON "C"."OrganizationId" = "TG"."OrganizationId"
                                                      LEFT JOIN "CollectionGroups" "CG" ON "C"."Id" = "CG"."CollectionId" AND "TG"."GroupId" = "CG"."GroupId"
                                                      WHERE "CG"."CollectionId" IS NULL;

                                                      -- Update "Group" to clear "AccessAll" flag and update "RevisionDate"
                                                      UPDATE "Group" "G"
                                                      SET "AccessAll" = false, "RevisionDate" = CURRENT_TIMESTAMP
                                                      WHERE "G"."Id" IN (SELECT "GroupId" FROM "TempGroupsAccessAll");

                                                  -- Step 2
                                                      -- Update existing rows in CollectionUsers
                                                      UPDATE "CollectionUsers" "target"
                                                      SET "ReadOnly" = false,
                                                          "HidePasswords" = false,
                                                          "Manage" = false
                                                      FROM "Collection" "C"
                                                      WHERE "target"."CollectionId" = "C"."Id"
                                                      AND "C"."OrganizationId" IN (SELECT "OrganizationId" FROM "TempUsersAccessAll")
                                                      AND "target"."OrganizationUserId" IN (SELECT "OrganizationUserId" FROM "TempUsersAccessAll");

                                                      -- Insert new rows into CollectionUsers
                                                      INSERT INTO "CollectionUsers" ("CollectionId", "OrganizationUserId", "ReadOnly", "HidePasswords", "Manage")
                                                      SELECT "C"."Id", "TU"."OrganizationUserId", false, false, false
                                                      FROM "Collection" "C"
                                                      INNER JOIN "TempUsersAccessAll" "TU" ON "C"."OrganizationId" = "TU"."OrganizationId"
                                                      LEFT JOIN "CollectionUsers" "target" ON "C"."Id" = "target"."CollectionId" AND "TU"."OrganizationUserId" = "target"."OrganizationUserId"
                                                      WHERE "target"."CollectionId" IS NULL;

                                                      -- Update "OrganizationUser" to clear "AccessAll" flag
                                                      UPDATE "OrganizationUser" "OU"
                                                      SET "AccessAll" = false, "RevisionDate" = CURRENT_TIMESTAMP
                                                      WHERE "OU"."Id" IN (SELECT "OrganizationUserId" FROM "TempUsersAccessAll");

                                                  -- Step 3
                                                      -- Update CollectionUsers with Manage = 1 using the temporary table
                                                      UPDATE "CollectionUsers" "CU"
                                                      SET "ReadOnly" = false,
                                                          "HidePasswords" = false,
                                                          "Manage" = true
                                                      FROM "TempUserManagers" "TUM"
                                                      WHERE "CU"."OrganizationUserId" = "TUM"."OrganizationUserId";

                                                      -- Insert rows to CollectionUsers with Manage = true using the temporary table
                                                      -- This is for orgUsers who are Managers / EditAssignedCollections but have access via a group
                                                      -- We cannot give the whole group Manage permissions so we have to give them a direct assignment
                                                      INSERT INTO "CollectionUsers" ("CollectionId", "OrganizationUserId", "ReadOnly", "HidePasswords", "Manage")
                                                      SELECT DISTINCT "CG"."CollectionId", "TUM"."OrganizationUserId", false, false, true
                                                      FROM "CollectionGroups" "CG"
                                                      INNER JOIN "GroupUser" "GU" ON "CG"."GroupId" = "GU"."GroupId"
                                                      INNER JOIN "TempUserManagers" "TUM" ON "GU"."OrganizationUserId" = "TUM"."OrganizationUserId"
                                                      WHERE NOT EXISTS (
                                                          SELECT 1 FROM "CollectionUsers" "CU"
                                                          WHERE "CU"."CollectionId" = "CG"."CollectionId" AND "CU"."OrganizationUserId" = "TUM"."OrganizationUserId"
                                                      );

                                                      -- Update "OrganizationUser" to migrate all OrganizationUsers with Manager role to User role
                                                      UPDATE "OrganizationUser" "OU"
                                                      SET "Type" = 2, "RevisionDate" = CURRENT_TIMESTAMP -- User
                                                      WHERE "OU"."Id" IN (SELECT "OrganizationUserId" FROM "TempUserManagers" WHERE "IsManager" = true);

                                                  -- Step 4
                                                      -- Update "User" "AccountRevisionDate" for each unique "OrganizationUserId"
                                                      UPDATE "User" "U"
                                                      SET "AccountRevisionDate" = CURRENT_TIMESTAMP
                                                      FROM "OrganizationUser" "OU"
                                                      WHERE "U"."Id" = "OU"."UserId"
                                                      AND "OU"."Id" IN (
                                                          SELECT "OrganizationUserId"
                                                          FROM "GroupUser"
                                                          WHERE "GroupId" IN (SELECT "GroupId" FROM "TempGroupsAccessAll")
                                                          UNION
                                                          SELECT "OrganizationUserId" FROM "TempUsersAccessAll"
                                                          UNION
                                                          SELECT "OrganizationUserId" FROM "TempUserManagers"
                                                      );

                                                  -- Step 5
                                                      -- Set "FlexibleCollections" = true for all organizations that have not yet been migrated.
                                                      UPDATE "Organization"
                                                      SET "FlexibleCollections" = true
                                                      WHERE "FlexibleCollections" = false;

                                              -- Step 5: Drop the temporary tables
                                              DROP TABLE IF EXISTS "TempGroupsAccessAll";
                                              DROP TABLE IF EXISTS "TempUsersAccessAll";
                                              DROP TABLE IF EXISTS "TempUserManagers";
                                              """;

    private readonly string _mysqlQuery = $"""
                                           -- Step 1: AccessAll migration for Groups
                                               -- Create a temporary table to store the groups with AccessAll = 1
                                               CREATE TEMPORARY TABLE IF NOT EXISTS `TempGroupsAccessAll` AS
                                               SELECT `G`.`Id` AS `GroupId`,
                                                      `G`.`OrganizationId`
                                               FROM `Group` `G`
                                               INNER JOIN `Organization` `O` ON `G`.`OrganizationId` = `O`.`Id`
                                               WHERE `O`.`FlexibleCollections` = 0 AND `G`.`AccessAll` = 1;

                                           -- Step 2: AccessAll migration for OrganizationUsers
                                               -- Create a temporary table to store the OrganizationUsers with AccessAll = 1
                                               CREATE TEMPORARY TABLE IF NOT EXISTS `TempUsersAccessAll` AS
                                               SELECT `OU`.`Id` AS `OrganizationUserId`,
                                                      `OU`.`OrganizationId`
                                               FROM `OrganizationUser` `OU`
                                               INNER JOIN `Organization` `O` ON `OU`.`OrganizationId` = `O`.`Id`
                                               WHERE `O`.`FlexibleCollections` = 0 AND `OU`.`AccessAll` = 1;

                                           -- Step 3: For all OrganizationUsers with Manager role or 'EditAssignedCollections' permission update their existing CollectionUsers rows and insert new rows with [Manage] = 1
                                           -- and finally update all OrganizationUsers with Manager role to User role
                                               -- Create a temporary table to store the OrganizationUsers with Manager role or 'EditAssignedCollections' permission
                                               CREATE TEMPORARY TABLE IF NOT EXISTS `TempUserManagers` AS
                                               SELECT `OU`.`Id` AS `OrganizationUserId`,
                                                      CASE WHEN `OU`.`Type` = 3 THEN 1 ELSE 0 END AS `IsManager`
                                               FROM `OrganizationUser` `OU`
                                               INNER JOIN `Organization` `O` ON `OU`.`OrganizationId` = `O`.`Id`
                                               WHERE `O`.`FlexibleCollections` = 0 AND
                                                     (`OU`.`Type` = 3 OR
                                                      (`OU`.`Type` = 4 AND
                                                       `OU`.`Permissions` IS NOT NULL AND
                                                       JSON_VALID(`OU`.`Permissions`) AND JSON_VALUE(`OU`.`Permissions`, '$.editAssignedCollections') = 'true'));

                                           -- Start transaction
                                           START TRANSACTION;
                                               -- Step 1
                                                   -- Update existing rows in `CollectionGroups`
                                                   UPDATE `CollectionGroups` `CG`
                                                   INNER JOIN `Collection` `C` ON `CG`.`CollectionId` = `C`.`Id`
                                                   INNER JOIN `TempGroupsAccessAll` `TG` ON `CG`.`GroupId` = `TG`.`GroupId`
                                                   SET `CG`.`ReadOnly` = 0,
                                                       `CG`.`HidePasswords` = 0,
                                                       `CG`.`Manage` = 0
                                                   WHERE `C`.`OrganizationId` = `TG`.`OrganizationId`;

                                                   -- Insert new rows into `CollectionGroups`
                                                   INSERT INTO `CollectionGroups` (`CollectionId`, `GroupId`, `ReadOnly`, `HidePasswords`, `Manage`)
                                                   SELECT `C`.`Id`, `TG`.`GroupId`, 0, 0, 0
                                                   FROM `Collection` `C`
                                                   INNER JOIN `TempGroupsAccessAll` `TG` ON `C`.`OrganizationId` = `TG`.`OrganizationId`
                                                   LEFT JOIN `CollectionGroups` `CG` ON `CG`.`CollectionId` = `C`.`Id` AND `CG`.`GroupId` = `TG`.`GroupId`
                                                   WHERE `CG`.`CollectionId` IS NULL;

                                                   -- Update `Group` to clear `AccessAll` flag and update `RevisionDate`
                                                   UPDATE `Group` `G`
                                                   SET `AccessAll` = 0, `RevisionDate` = UTC_TIMESTAMP()
                                                   WHERE `G`.`Id` IN (SELECT `GroupId` FROM `TempGroupsAccessAll`);

                                               -- Step 2
                                                   -- Update existing rows in `CollectionUsers`
                                                   UPDATE `CollectionUsers` `target`
                                                   INNER JOIN `Collection` `C` ON `target`.`CollectionId` = `C`.`Id`
                                                   INNER JOIN `TempUsersAccessAll` `TU`
                                                       ON `C`.`OrganizationId` = `TU`.`OrganizationId` AND `target`.`OrganizationUserId` = `TU`.`OrganizationUserId`
                                                   SET `target`.`ReadOnly` = 0,
                                                       `target`.`HidePasswords` = 0,
                                                       `target`.`Manage` = 0;

                                                   -- Insert new rows into `CollectionUsers`
                                                   INSERT INTO `CollectionUsers` (`CollectionId`, `OrganizationUserId`, `ReadOnly`, `HidePasswords`, `Manage`)
                                                   SELECT `C`.`Id`, `TU`.`OrganizationUserId`, 0, 0, 0
                                                   FROM `Collection` `C`
                                                   INNER JOIN `TempUsersAccessAll` `TU` ON `C`.`OrganizationId` = `TU`.`OrganizationId`
                                                   LEFT JOIN `CollectionUsers` `target`
                                                       ON `target`.`CollectionId` = `C`.`Id` AND `target`.`OrganizationUserId` = `TU`.`OrganizationUserId`
                                                   WHERE `target`.`CollectionId` IS NULL;

                                                   -- Update `OrganizationUser` to clear `AccessAll` flag
                                                   UPDATE `OrganizationUser` `OU`
                                                   SET `AccessAll` = 0, `RevisionDate` = UTC_TIMESTAMP()
                                                   WHERE `OU`.`Id` IN (SELECT `OrganizationUserId` FROM `TempUsersAccessAll`);

                                               -- Step 3
                                                   -- Update `CollectionUsers` with `Manage` = 1 using the temporary table
                                                   UPDATE `CollectionUsers` `CU`
                                                   INNER JOIN `TempUserManagers` `TUM` ON `CU`.`OrganizationUserId` = `TUM`.`OrganizationUserId`
                                                   SET `CU`.`ReadOnly` = 0,
                                                       `CU`.`HidePasswords` = 0,
                                                       `CU`.`Manage` = 1;

                                                   -- Insert rows to `CollectionUsers` with `Manage` = 1 using the temporary table
                                                   -- This is for orgUsers who are Managers / EditAssignedCollections but have access via a group
                                                   -- We cannot give the whole group Manage permissions so we have to give them a direct assignment
                                                   INSERT INTO `CollectionUsers` (`CollectionId`, `OrganizationUserId`, `ReadOnly`, `HidePasswords`, `Manage`)
                                                   SELECT DISTINCT `CG`.`CollectionId`, `TUM`.`OrganizationUserId`, 0, 0, 1
                                                   FROM `CollectionGroups` `CG`
                                                   INNER JOIN `GroupUser` `GU` ON `CG`.`GroupId` = `GU`.`GroupId`
                                                   INNER JOIN `TempUserManagers` `TUM` ON `GU`.`OrganizationUserId` = `TUM`.`OrganizationUserId`
                                                   WHERE NOT EXISTS (
                                                       SELECT 1 FROM `CollectionUsers` `CU`
                                                       WHERE `CU`.`CollectionId` = `CG`.`CollectionId` AND `CU`.`OrganizationUserId` = `TUM`.`OrganizationUserId`
                                                   );

                                                   -- Update `OrganizationUser` to migrate all OrganizationUsers with Manager role to User role
                                                   UPDATE `OrganizationUser` `OU`
                                                   SET `OU`.`Type` = 2, `OU`.`RevisionDate` = UTC_TIMESTAMP() -- User
                                                   WHERE `OU`.`Id` IN (SELECT `OrganizationUserId` FROM `TempUserManagers` WHERE `IsManager` = 1);

                                               -- Step 4
                                                   -- Update `User` `AccountRevisionDate` for each unique `OrganizationUserId`
                                                   UPDATE `User` `U`
                                                   INNER JOIN `OrganizationUser` `OU` ON `U`.`Id` = `OU`.`UserId`
                                                   INNER JOIN (
                                                       -- Step 1
                                                       SELECT `GU`.`OrganizationUserId`
                                                       FROM `GroupUser` `GU`
                                                       INNER JOIN `TempGroupsAccessAll` `TG` ON `GU`.`GroupId` = `TG`.`GroupId`

                                                       UNION

                                                       -- Step 2
                                                       SELECT `OrganizationUserId`
                                                       FROM `TempUsersAccessAll`

                                                       UNION

                                                       -- Step 3
                                                       SELECT `OrganizationUserId`
                                                       FROM `TempUserManagers`
                                                   ) AS `CombinedOrgUsers` ON `OU`.`Id` = `CombinedOrgUsers`.`OrganizationUserId`
                                                   SET `U`.`AccountRevisionDate` = UTC_TIMESTAMP();

                                               -- Step 5
                                                   -- Set `FlexibleCollections` = 1 for all organizations that have not yet been migrated.
                                                   UPDATE `Organization`
                                                   SET `FlexibleCollections` = 1
                                                   WHERE `FlexibleCollections` = 0;

                                           -- Commit transaction
                                           COMMIT;

                                           -- Step 5: Drop the temporary tables
                                           DROP TEMPORARY TABLE IF EXISTS `TempGroupsAccessAll`;
                                           DROP TEMPORARY TABLE IF EXISTS `TempUsersAccessAll`;
                                           DROP TEMPORARY TABLE IF EXISTS `TempUserManagers`;
                                           """;

    private readonly string _sqliteQuery = $"""
                                  -- Step 1: AccessAll migration for Groups
                                      -- Create a temporary table to store the groups with AccessAll = 1
                                      CREATE TEMPORARY TABLE IF NOT EXISTS "TempGroupsAccessAll" AS
                                      SELECT "G"."Id" AS "GroupId",
                                             "G"."OrganizationId"
                                      FROM "Group" "G"
                                      INNER JOIN "Organization" "O" ON "G"."OrganizationId" = "O"."Id"
                                      WHERE "O"."FlexibleCollections" = 0 AND "G"."AccessAll" = 1;

                                  -- Step 2: AccessAll migration for OrganizationUsers
                                      -- Create a temporary table to store the OrganizationUsers with AccessAll = 1
                                      CREATE TEMPORARY TABLE IF NOT EXISTS "TempUsersAccessAll" AS
                                      SELECT "OU"."Id" AS "OrganizationUserId",
                                             "OU"."OrganizationId"
                                      FROM "OrganizationUser" "OU"
                                      INNER JOIN "Organization" "O" ON "OU"."OrganizationId" = "O"."Id"
                                      WHERE "O"."FlexibleCollections" = 0 AND "OU"."AccessAll" = 1;

                                  -- Step 3: For all OrganizationUsers with Manager role or 'EditAssignedCollections' permission update their existing CollectionUsers rows and insert new rows with [Manage] = 1
                                  -- and finally update all OrganizationUsers with Manager role to User role
                                      -- Create a temporary table to store the OrganizationUsers with Manager role or 'EditAssignedCollections' permission
                                      CREATE TEMPORARY TABLE IF NOT EXISTS "TempUserManagers" AS
                                      SELECT "OU"."Id" AS "OrganizationUserId",
                                             CASE WHEN "OU"."Type" = 3 THEN 1 ELSE 0 END AS "IsManager"
                                      FROM "OrganizationUser" "OU"
                                      INNER JOIN "Organization" "O" ON "OU"."OrganizationId" = "O"."Id"
                                      WHERE "O"."FlexibleCollections" = 0 AND
                                          ("OU"."Type" = 3 OR
                                           ("OU"."Type" = 4 AND
                                            "OU"."Permissions" IS NOT NULL AND
                                            JSON_VALID("OU"."Permissions") AND JSON_EXTRACT(ou."Permissions", '$.editAssignedCollections') = 'true'));

                                  -- Step 1
                                      -- Update existing rows in "CollectionGroups"
                                      UPDATE "CollectionGroups"
                                      SET
                                          "ReadOnly" = 0,
                                          "HidePasswords" = 0,
                                          "Manage" = 0
                                      WHERE EXISTS (
                                          SELECT 1
                                          FROM "Collection" "C"
                                          INNER JOIN "TempGroupsAccessAll" "TG" ON "CollectionGroups"."GroupId" = "TG"."GroupId"
                                          WHERE "CollectionGroups"."CollectionId" = "C"."Id" AND C."OrganizationId" = "TG"."OrganizationId"
                                      );

                                      -- Insert new rows into "CollectionGroups"
                                      INSERT INTO "CollectionGroups" ("CollectionId", "GroupId", "ReadOnly", "HidePasswords", "Manage")
                                      SELECT "C"."Id", "TG"."GroupId", 0, 0, 0
                                      FROM "Collection" "C"
                                      INNER JOIN "TempGroupsAccessAll" "TG" ON "C"."OrganizationId" = "TG"."OrganizationId"
                                      LEFT JOIN "CollectionGroups" "CG" ON "CG"."CollectionId" = "C"."Id" AND "CG"."GroupId" = "TG"."GroupId"
                                      WHERE "CG"."CollectionId" IS NULL;

                                      -- Update "Group" to clear "AccessAll" flag and update "RevisionDate"
                                      UPDATE "Group"
                                      SET "AccessAll" = 0, "RevisionDate" = CURRENT_TIMESTAMP
                                      WHERE "Id" IN (SELECT "GroupId" FROM "TempGroupsAccessAll");

                                  -- Step 2
                                      -- Update existing rows in "CollectionUsers"
                                      UPDATE "CollectionUsers"
                                      SET
                                          "ReadOnly" = 0,
                                          "HidePasswords" = 0,
                                          "Manage" = 0
                                      WHERE "CollectionId" IN (
                                          SELECT "C"."Id"
                                          FROM "Collection" "C"
                                          INNER JOIN "TempUsersAccessAll" "TU" ON "C"."OrganizationId" = "TU"."OrganizationId"
                                      );

                                      -- Insert new rows into "CollectionUsers"
                                      INSERT INTO "CollectionUsers" ("CollectionId", "OrganizationUserId", "ReadOnly", "HidePasswords", "Manage")
                                      SELECT "C"."Id", "TU"."OrganizationUserId", 0, 0, 0
                                      FROM "Collection" "C"
                                      INNER JOIN "TempUsersAccessAll" "TU" ON "C"."OrganizationId" = "TU"."OrganizationId"
                                      LEFT JOIN "CollectionUsers" "target"
                                          ON "target"."CollectionId" = "C"."Id" AND "target"."OrganizationUserId" = "TU"."OrganizationUserId"
                                      WHERE "target"."CollectionId" IS NULL;

                                      -- Update "OrganizationUser" to clear "AccessAll" flag
                                      UPDATE "OrganizationUser"
                                      SET "AccessAll" = 0, "RevisionDate" = CURRENT_TIMESTAMP
                                      WHERE "Id" IN (SELECT "OrganizationUserId" FROM "TempUsersAccessAll");

                                  -- Step 3
                                      -- Update "CollectionUsers" with "Manage" = 1 using the temporary table
                                      UPDATE "CollectionUsers"
                                      SET
                                          "ReadOnly" = 0,
                                          "HidePasswords" = 0,
                                          "Manage" = 1
                                      WHERE "OrganizationUserId" IN (SELECT "OrganizationUserId" FROM "TempUserManagers");

                                      -- Insert rows to "CollectionUsers" with "Manage" = 1 using the temporary table
                                      -- This is for orgUsers who are Managers / EditAssignedCollections but have access via a group
                                      -- We cannot give the whole group Manage permissions so we have to give them a direct assignment
                                      INSERT INTO "CollectionUsers" ("CollectionId", "OrganizationUserId", "ReadOnly", "HidePasswords", "Manage")
                                      SELECT DISTINCT "CG"."CollectionId", "TUM"."OrganizationUserId", 0, 0, 1
                                      FROM "CollectionGroups" "CG"
                                      INNER JOIN "GroupUser" "GU" ON "CG"."GroupId" = "GU"."GroupId"
                                      INNER JOIN "TempUserManagers" "TUM" ON "GU"."OrganizationUserId" = "TUM"."OrganizationUserId"
                                      WHERE NOT EXISTS (
                                          SELECT 1 FROM "CollectionUsers" "CU"
                                          WHERE "CU"."CollectionId" = "CG"."CollectionId" AND "CU"."OrganizationUserId" = "TUM"."OrganizationUserId"
                                      );

                                      -- Update "OrganizationUser" to migrate all OrganizationUsers with Manager role to User role
                                      UPDATE "OrganizationUser"
                                      SET "Type" = 2, "RevisionDate" = CURRENT_TIMESTAMP -- User
                                      WHERE "Id" IN (SELECT "OrganizationUserId" FROM "TempUserManagers" WHERE "IsManager" = 1);

                                  -- Step 4
                                      -- Update "User" "AccountRevisionDate" for each unique "OrganizationUserId"
                                      UPDATE "User"
                                      SET "AccountRevisionDate" = CURRENT_TIMESTAMP
                                      WHERE "Id" IN (
                                          SELECT DISTINCT "OU"."UserId"
                                          FROM "OrganizationUser" "OU"
                                                   INNER JOIN (
                                              -- Step 1
                                              SELECT "GU"."OrganizationUserId"
                                              FROM "GroupUser" "GU"
                                                       INNER JOIN "TempGroupsAccessAll" "TG" ON "GU"."GroupId" = "TG"."GroupId"

                                              UNION

                                              -- Step 2
                                              SELECT "OrganizationUserId"
                                              FROM "TempUsersAccessAll"

                                              UNION

                                              -- Step 3
                                              SELECT "OrganizationUserId"
                                              FROM "TempUserManagers"
                                          ) AS "CombinedOrgUsers" ON "OU"."Id" = "CombinedOrgUsers"."OrganizationUserId"
                                      );

                                  -- Step 5
                                      -- Set "FlexibleCollections" = 1 for all organizations that have not yet been migrated.
                                      UPDATE "Organization"
                                      SET "FlexibleCollections" = 1
                                      WHERE "FlexibleCollections" = 0;


                                  -- Step 6: Drop the temporary tables
                                  DROP TABLE IF EXISTS "TempGroupsAccessAll";
                                  DROP TABLE IF EXISTS "TempUsersAccessAll";
                                  DROP TABLE IF EXISTS "TempUserManagers";
                                  """;
}
