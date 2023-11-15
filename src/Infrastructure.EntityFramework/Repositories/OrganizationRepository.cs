﻿using AutoMapper;
using AutoMapper.QueryableExtensions;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Organization = Bit.Infrastructure.EntityFramework.Models.Organization;

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
            organization.UseSecretsManager = organization.UseSecretsManager && !organization.SecretsManagerBeta;

            return organization;
        }
    }

    public async Task<ICollection<Core.Entities.Organization>> GetManyByEnabledAsync()
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var organizations = await GetDbSet(dbContext).Where(e => e.Enabled).ToListAsync();
            var list = Mapper.Map<List<Core.Entities.Organization>>(organizations);

            foreach (Core.Entities.Organization entity in list)
            {
                entity.UseSecretsManager = organization.UseSecretsManager && !organization.SecretsManagerBeta;
            }

            return list;
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
            var list = Mapper.Map<List<Core.Entities.Organization>>(organizations);

            foreach (Core.Entities.Organization entity in list)
            {
                entity.UseSecretsManager = organization.UseSecretsManager && !organization.SecretsManagerBeta;
            }

            return list;
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
            var list = Mapper.Map<List<Core.Entities.Organization>>(organizations);

            foreach (Core.Entities.Organization entity in list)
            {
                entity.UseSecretsManager = organization.UseSecretsManager && !organization.SecretsManagerBeta;
            }

            return list;
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
                UsePolicies = e.UsePolicies
            }).ToListAsync();
        }
    }

    public async Task<ICollection<Core.Entities.Organization>> SearchUnassignedToProviderAsync(string name, string ownerEmail, int skip, int take)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var query = from o in dbContext.Organizations
                    where o.PlanType >= PlanType.TeamsMonthly2020 && o.PlanType <= PlanType.EnterpriseAnnually &&
                          !dbContext.ProviderOrganizations.Any(po => po.OrganizationId == o.Id) &&
                          (string.IsNullOrWhiteSpace(name) || EF.Functions.Like(o.Name, $"%{name}%"))
                    select o;

        if (string.IsNullOrWhiteSpace(ownerEmail))
        {
            var list = await query.OrderByDescending(o => o.CreationDate)
                .Skip(skip)
                .Take(take)
                .ToArrayAsync();

            foreach (Core.Entities.Organization entity in list)
            {
                entity.UseSecretsManager = organization.UseSecretsManager && !organization.SecretsManagerBeta;
            }

            return list;
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

        var list = await query.OrderByDescending(o => o.CreationDate).Skip(skip).Take(take).ToArrayAsync();

        foreach (Core.Entities.Organization entity in list)
        {
            entity.UseSecretsManager = organization.UseSecretsManager && !organization.SecretsManagerBeta;
        }

        return list;
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
            await dbContext.Ciphers.Where(c => c.UserId == null && c.OrganizationId == organization.Id)
                .ExecuteDeleteAsync();
            await deleteCiphersTransaction.CommitAsync();

            var organizationDeleteTransaction = await dbContext.Database.BeginTransactionAsync();
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

    public async Task<Core.Entities.Organization> GetByLicenseKeyAsync(string licenseKey)
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

    public override async Task<Core.Entities.Organization> GetByIdAsync(Guid id)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var entity = await GetDbSet(dbContext).FindAsync(id);
            var result = Mapper.Map<Core.Entities.Organization>(entity);
            result.UseSecretsManager = result.UseSecretsManager && !result.SecretsManagerBeta;

            return result;
        }
    }
}
