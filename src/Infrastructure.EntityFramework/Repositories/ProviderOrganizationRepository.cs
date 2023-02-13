using AutoMapper;
using Bit.Core.Entities.Provider;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories.Queries;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.Repositories;

public class ProviderOrganizationRepository :
    Repository<ProviderOrganization, Models.ProviderOrganization, Guid>, IProviderOrganizationRepository
{
    public ProviderOrganizationRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, context => context.ProviderOrganizations)
    { }

    public async Task<ICollection<ProviderOrganization>> CreateWithManyOrganizations(ProviderOrganization providerOrganization, IEnumerable<Guid> organizationIds)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var insert = from o in dbContext.Organizations
                         where organizationIds.Contains(o.Id) &&
                               !dbContext.ProviderOrganizations.Any(po => po.ProviderId == providerOrganization.ProviderId && po.OrganizationId == o.Id)
                         select new ProviderOrganization
                         {
                             ProviderId = providerOrganization.ProviderId,
                             OrganizationId = o.Id,
                             Key = providerOrganization.Key,
                             Settings = providerOrganization.Settings
                         };

            await dbContext.AddRangeAsync(insert);
            await dbContext.SaveChangesAsync();

            return insert.ToList();
        }
    }

    public async Task<ICollection<ProviderOrganizationUnassignedOrganizationDetails>> SearchAsync(string name, string ownerEmail, int skip, int take)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = (from o in dbContext.Organizations
                         join ou in dbContext.OrganizationUsers
                             on o.Id equals ou.OrganizationId
                         join u in dbContext.Users
                             on ou.UserId equals u.Id
                         where o.PlanType >= PlanType.TeamsMonthly && o.PlanType <= PlanType.EnterpriseAnnually &&
                               !dbContext.ProviderOrganizations.Any(po => po.OrganizationId == o.Id) &&
                               ou.Type == OrganizationUserType.Owner &&
                               (string.IsNullOrWhiteSpace(name) || EF.Functions.Like(o.Name, $"%{name}%")) &&
                               (string.IsNullOrWhiteSpace(ownerEmail) || u.Email == ownerEmail)
                         orderby o.CreationDate descending
                         select new { o, u }).Skip(skip).Take(take).Select(r => new ProviderOrganizationUnassignedOrganizationDetails
                         {
                             OrganizationId = r.o.Id,
                             Name = r.o.Name,
                             OwnerEmail = r.u.Email,
                             PlanType = r.o.PlanType
                         });

            return await query.ToArrayAsync();
        }
    }

    public async Task<ICollection<ProviderOrganizationOrganizationDetails>> GetManyDetailsByProviderAsync(Guid providerId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = new ProviderOrganizationOrganizationDetailsReadByProviderIdQuery(providerId);
            var data = await query.Run(dbContext).ToListAsync();
            return data;
        }
    }

    public async Task<ProviderOrganization> GetByOrganizationId(Guid organizationId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        return await GetDbSet(dbContext).Where(po => po.OrganizationId == organizationId).FirstOrDefaultAsync();
    }
}
