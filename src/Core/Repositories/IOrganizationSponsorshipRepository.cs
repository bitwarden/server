using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Entities;

namespace Bit.Core.Repositories
{
    public interface IOrganizationSponsorshipRepository : IRepository<OrganizationSponsorship, Guid>
    {
        Task<IEnumerable<OrganizationSponsorship>> GetBySponsoringOrganizationAsync(Guid sponsoringOrganizationId);
        Task<OrganizationSponsorship> GetBySponsoringOrganizationUserIdAsync(Guid sponsoringOrganizationUserId);
        Task<OrganizationSponsorship> GetBySponsoredOrganizationIdAsync(Guid sponsoredOrganizationId);
        Task<DateTime?> GetLatestSyncDateBySponsoringOrganizationIdAsync(Guid sponsoringOrganizationId);
    }
}
