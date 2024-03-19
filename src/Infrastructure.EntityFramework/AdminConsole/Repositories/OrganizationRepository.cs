using AutoMapper;
using AutoMapper.QueryableExtensions;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Models;
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

    public async Task EnableCollectionEnhancements(Guid organizationId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        try
        {
            // Step 1: AccessAll migration for Groups
            var groupIdsWithAccessAll = dbContext.Groups
                .Where(g => g.OrganizationId == organizationId && g.AccessAll == true)
                .Select(g => g.Id)
                .ToList();

            // Update existing CollectionGroup rows
            dbContext.CollectionGroups
                .Where(cg =>
                    groupIdsWithAccessAll.Contains(cg.GroupId) &&
                    cg.Collection.OrganizationId == organizationId)
                .ToList()
                .ForEach(cg =>
                {
                    cg.ReadOnly = false;
                    cg.HidePasswords = false;
                    cg.Manage = false;
                });

            // Insert new rows into CollectionGroup
            foreach (var group in groupIdsWithAccessAll)
            {
                var newCollectionGroups = dbContext.Collections
                    .Where(c =>
                        c.OrganizationId == organizationId &&
                        !dbContext.CollectionGroups.Any(cg => cg.CollectionId == c.Id && cg.GroupId == group))
                    .Select(c => new CollectionGroup
                    {
                        CollectionId = c.Id,
                        GroupId = group,
                        ReadOnly = false,
                        HidePasswords = false,
                        Manage = false
                    })
                    .ToList();
                dbContext.CollectionGroups.AddRange(newCollectionGroups);
            }

            // Update Group to clear AccessAll flag
            dbContext.Groups
                .Where(g => groupIdsWithAccessAll.Contains(g.Id))
                .ToList()
                .ForEach(g => g.AccessAll = false);

            // Save changes for Step 1
            await dbContext.SaveChangesAsync();

            // Step 2: AccessAll migration for OrganizationUsers
            var orgUserIdsWithAccessAll = dbContext.OrganizationUsers
                .Where(ou => ou.OrganizationId == organizationId && ou.AccessAll == true)
                .Select(ou => ou.Id)
                .ToList();

            // Update existing CollectionUser rows
            dbContext.CollectionUsers
                .Where(cu =>
                    orgUserIdsWithAccessAll.Contains(cu.OrganizationUserId) &&
                    cu.Collection.OrganizationId == organizationId)
                .ToList()
                .ForEach(cu =>
                {
                    cu.ReadOnly = false;
                    cu.HidePasswords = false;
                    cu.Manage = false;
                });

            // Insert new rows into CollectionUser
            foreach (var organizationUser in orgUserIdsWithAccessAll)
            {
                var newCollectionUsers = dbContext.Collections
                    .Where(c =>
                        c.OrganizationId == organizationId &&
                        !dbContext.CollectionUsers.Any(cu => cu.CollectionId == c.Id && cu.OrganizationUserId == organizationUser))
                    .Select(c => new CollectionUser
                    {
                        CollectionId = c.Id,
                        OrganizationUserId = organizationUser,
                        ReadOnly = false,
                        HidePasswords = false,
                        Manage = false
                    })
                    .ToList();
                dbContext.CollectionUsers.AddRange(newCollectionUsers);
            }

            // Update OrganizationUser to clear AccessAll flag
            dbContext.OrganizationUsers
                .Where(ou => orgUserIdsWithAccessAll.Contains(ou.Id))
                .ToList()
                .ForEach(ou => ou.AccessAll = false);

            // Save changes for Step 2
            await dbContext.SaveChangesAsync();

            // Step 3: For all OrganizationUsers with Manager role or 'EditAssignedCollections' permission
            // update their existing CollectionUser rows and insert new rows with [Manage] = 1
            // and finally update all OrganizationUsers with Manager role to User role
            var managerOrgUsersIds = dbContext.OrganizationUsers
                .Where(ou =>
                    ou.OrganizationId == organizationId &&
                    (ou.Type == OrganizationUserType.Manager ||
                     (ou.Permissions != null && EF.Functions.Like(ou.Permissions, "%\"editAssignedCollections\":true%"))))
                .Select(ou => ou.Id)
                .ToList();

            // Update CollectionUser rows with Manage = true
            dbContext.CollectionUsers
                .Where(cu => managerOrgUsersIds.Contains(cu.OrganizationUserId))
                .ToList()
                .ForEach(cu =>
                {
                    cu.ReadOnly = false;
                    cu.HidePasswords = false;
                    cu.Manage = true;
                });

            // Insert rows into CollectionUser with Manage = true
            foreach (var manager in managerOrgUsersIds)
            {
                var newCollectionUsersWithManage = (from cg in dbContext.CollectionGroups
                                                    join gu in dbContext.GroupUsers on cg.GroupId equals gu.GroupId
                                                    where gu.OrganizationUserId == manager &&
                                                          !dbContext.CollectionUsers.Any(cu =>
                                                              cu.CollectionId == cg.CollectionId &&
                                                              cu.OrganizationUserId == manager)
                                                    select new CollectionUser
                                                    {
                                                        CollectionId = cg.CollectionId,
                                                        OrganizationUserId = manager,
                                                        ReadOnly = false,
                                                        HidePasswords = false,
                                                        Manage = true
                                                    }).Distinct().ToList();
                dbContext.CollectionUsers.AddRange(newCollectionUsersWithManage);
            }

            // Update OrganizationUser to migrate Managers to User role
            dbContext.OrganizationUsers
                .Where(ou =>
                    managerOrgUsersIds.Contains(ou.Id) &&
                    ou.Type == OrganizationUserType.Manager)
                .ToList()
                .ForEach(ou => ou.Type = OrganizationUserType.User);

            // Save changes for Step 3
            await dbContext.SaveChangesAsync();

            // Step 4: Bump AccountRevisionDate for all OrganizationUsers updated in the previous steps
            // Get all OrganizationUserIds that have AccessAll from Group
            var accessAllGroupOrgUserIds = dbContext.GroupUsers
                .Where(gu => groupIdsWithAccessAll.Contains(gu.GroupId))
                .Select(gu => gu.OrganizationUserId)
                .ToList();

            // Combine and union the distinct OrganizationUserIds from all steps into a single variable
            var orgUsersToBump = accessAllGroupOrgUserIds
                .Union(orgUserIdsWithAccessAll)
                .Union(managerOrgUsersIds)
                .Distinct()
                .ToList();

            await dbContext.UserBumpAccountRevisionDateByOrganizationUserIdsAsync(orgUsersToBump);

            // Save changes for Step 4
            await dbContext.SaveChangesAsync();

            await transaction.CommitAsync();
        }
        catch
        {
            // Rollback transaction
            await transaction.RollbackAsync();
            throw new Exception("Error occurred. Rolling back transaction.");
        }
    }
}
