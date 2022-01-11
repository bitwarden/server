using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.Repositories
{
    public class OrganizationSponsorshipRepository : Repository<Core.Entities.OrganizationSponsorship, OrganizationSponsorship, Guid>, IOrganizationSponsorshipRepository
    {
        public OrganizationSponsorshipRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
            : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.OrganizationSponsorships)
        { }

        public async Task<Core.Entities.OrganizationSponsorship> GetByOfferedToEmailAsync(string email)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var orgSponsorship = await GetDbSet(dbContext).Where(e => e.OfferedToEmail == email)
                    .FirstOrDefaultAsync();
                return orgSponsorship;
            }
        }

        public async Task<Core.Entities.OrganizationSponsorship> GetBySponsoredOrganizationIdAsync(Guid sponsoredOrganizationId)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var orgSponsorship = await GetDbSet(dbContext).Where(e => e.SponsoredOrganizationId == sponsoredOrganizationId)
                    .FirstOrDefaultAsync();
                return orgSponsorship;
            }
        }

        public async Task<Core.Entities.OrganizationSponsorship> GetBySponsoringOrganizationUserIdAsync(Guid sponsoringOrganizationUserId)
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
