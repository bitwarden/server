using AutoMapper;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.Repositories;

public class OrganizationRepository : Repository<Core.Entities.Organization, Organization, Guid>, IOrganizationRepository
{
    public OrganizationRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.Organizations)
    { }

    public async Task<Core.Entities.Organization> GetByIdentifierAsync(string identifier)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var organization = await GetDbSet(dbContext).Where(e => e.Identifier == identifier)
                .FirstOrDefaultAsync();
            return organization;
        }
    }

    public async Task<ICollection<Core.Entities.Organization>> GetManyByEnabledAsync()
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var organizations = await GetDbSet(dbContext).Where(e => e.Enabled).ToListAsync();
            return Mapper.Map<List<Core.Entities.Organization>>(organizations);
        }
    }

    public async Task<ICollection<Core.Entities.Organization>> GetManyByUserIdAsync(Guid userId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var organizations = await GetDbSet(dbContext)
                .Select(e => e.OrganizationUsers
                    .Where(ou => ou.UserId == userId)
                    .Select(ou => ou.Organization))
                .ToListAsync();
            return Mapper.Map<List<Core.Entities.Organization>>(organizations);
        }
    }

    public async Task<ICollection<Core.Entities.Organization>> SearchAsync(string name, string userEmail,
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
            return Mapper.Map<List<Core.Entities.Organization>>(organizations);
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
                UseCustomPermissions = e.UseCustomPermissions
            }).ToListAsync();
        }
    }

    public async Task<ICollection<Core.Entities.Organization>> SearchUnassignedToProviderAsync(string name, string ownerEmail, int skip, int take)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = (from o in dbContext.Organizations
                         let ownerEmails = (from ou in dbContext.OrganizationUsers
                                            join u in dbContext.Users
                                                on ou.UserId equals u.Id
                                            where ou.OrganizationId == o.Id && ou.Type == OrganizationUserType.Owner
                                            select u.Email).ToList()
                         where o.PlanType >= PlanType.TeamsMonthly && o.PlanType <= PlanType.EnterpriseAnnually &&
                               !dbContext.ProviderOrganizations.Any(po => po.OrganizationId == o.Id) &&
                               (string.IsNullOrWhiteSpace(name) || EF.Functions.Like(o.Name, $"%{name}%")) &&
                               (string.IsNullOrWhiteSpace(ownerEmail) || ownerEmails.Any(email => email == ownerEmail))
                         orderby o.CreationDate descending
                         select o)
                .Skip(skip).Take(take);

            return await query.ToArrayAsync();
        }
    }

    public async Task UpdateStorageAsync(Guid id)
    {
        await OrganizationUpdateStorage(id);
    }

    public override async Task DeleteAsync(Core.Entities.Organization organization)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            await dbContext.UserBumpAccountRevisionDateByOrganizationIdAsync(organization.Id);
            var deleteCiphersTransaction = await dbContext.Database.BeginTransactionAsync();
            dbContext.Ciphers.RemoveRange(
                dbContext.Ciphers.Where(c => c.UserId == null && c.OrganizationId == organization.Id));
            await deleteCiphersTransaction.CommitAsync();
            var organizationDeleteTransaction = await dbContext.Database.BeginTransactionAsync();
            dbContext.SsoUsers.RemoveRange(dbContext.SsoUsers.Where(su => su.OrganizationId == organization.Id));
            dbContext.SsoConfigs.RemoveRange(dbContext.SsoConfigs.Where(sc => sc.OrganizationId == organization.Id));
            var collectionUsers = from cu in dbContext.CollectionUsers
                                  join ou in dbContext.OrganizationUsers on cu.OrganizationUserId equals ou.Id
                                  where ou.OrganizationId == organization.Id
                                  select cu;
            dbContext.CollectionUsers.RemoveRange(collectionUsers);
            dbContext.OrganizationUsers.RemoveRange(
                dbContext.OrganizationUsers.Where(ou => ou.OrganizationId == organization.Id));
            dbContext.ProviderOrganizations.RemoveRange(
                dbContext.ProviderOrganizations.Where(po => po.OrganizationId == organization.Id));

            // The below section are 3 SPROCS in SQL Server but are only called by here
            dbContext.OrganizationApiKeys.RemoveRange(
                dbContext.OrganizationApiKeys.Where(oa => oa.OrganizationId == organization.Id));
            dbContext.OrganizationConnections.RemoveRange(
                dbContext.OrganizationConnections.Where(oc => oc.OrganizationId == organization.Id));
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
}
