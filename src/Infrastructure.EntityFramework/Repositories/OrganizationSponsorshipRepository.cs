using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TableModel = Bit.Core.Models.Table;

namespace Bit.Infrastructure.EntityFramework.Repositories
{
    public class OrganizationSponsorshipRepository : Repository<TableModel.OrganizationSponsorship, OrganizationSponsorship, Guid>, IOrganizationSponsorshipRepository
    {
        public OrganizationSponsorshipRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper) 
            : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.OrganizationSponsorships)
        { }

        public async Task<TableModel.OrganizationSponsorship> GetByOfferedToEmailAsync(string email)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var orgSponsorship = await GetDbSet(dbContext).Where(e => e.OfferedToEmail == email)
                    .FirstOrDefaultAsync();
                return orgSponsorship;
            }
        }

        public async Task<TableModel.OrganizationSponsorship> GetBySponsoredOrganizationIdAsync(Guid sponsoredOrganizationId)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var orgSponsorship = await GetDbSet(dbContext).Where(e => e.SponsoredOrganizationId == sponsoredOrganizationId)
                    .FirstOrDefaultAsync();
                return orgSponsorship;
            }
        }

        public async Task<TableModel.OrganizationSponsorship> GetBySponsoringOrganizationUserIdAsync(Guid sponsoringOrganizationUserId)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var orgSponsorship = await GetDbSet(dbContext).Where(e => e.SponsoringOrganizationUserId == sponsoringOrganizationUserId)
                    .FirstOrDefaultAsync();
                return orgSponsorship;
            }
        }
    }
}
