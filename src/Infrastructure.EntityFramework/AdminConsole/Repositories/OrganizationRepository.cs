using AutoMapper;
using AutoMapper.QueryableExtensions;
using Bit.Core.Billing.Enums;
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
                .SelectMany(e => e.OrganizationUsers
                    .Where(ou => ou.UserId == userId))
                .Include(ou => ou.Organization)
                .Select(ou => ou.Organization)
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
                LimitCollectionCreation = e.LimitCollectionCreation,
                LimitCollectionDeletion = e.LimitCollectionDeletion,
                AllowAdminAccessToAllCollectionItems = e.AllowAdminAccessToAllCollectionItems
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

            await dbContext.NotificationStatuses.Where(ns => ns.Notification.OrganizationId == organization.Id)
                .ExecuteDeleteAsync();
            await dbContext.Notifications.Where(n => n.OrganizationId == organization.Id)
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

    public async Task<Core.AdminConsole.Entities.Organization> GetByClaimedUserDomainAsync(Guid userId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);

            var query = from u in dbContext.Users
                        join ou in dbContext.OrganizationUsers on u.Id equals ou.UserId
                        join o in dbContext.Organizations on ou.OrganizationId equals o.Id
                        join od in dbContext.OrganizationDomains on ou.OrganizationId equals od.OrganizationId
                        where u.Id == userId
                              && od.VerifiedDate != null
                              && u.Email.ToLower().EndsWith("@" + od.DomainName.ToLower())
                        select o;

            return await query.FirstOrDefaultAsync();
        }
    }

    public Task EnableCollectionEnhancements(Guid organizationId)
    {
        throw new NotImplementedException("Collection enhancements migration is not yet supported for Entity Framework.");
    }
}
