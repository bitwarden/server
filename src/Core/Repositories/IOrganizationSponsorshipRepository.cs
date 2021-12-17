using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Models.Table;

namespace Bit.Core.Repositories
{
    public interface IOrganizationSponsorshipRepository : IRepository<OrganizationSponsorship, Guid>
    {
        Task<OrganizationSponsorship> GetBySponsoringOrganizationUserIdAsync(Guid sponsoringOrganizationUserId);
        Task<OrganizationSponsorship> GetBySponsoredOrganizationIdAsync(Guid sponsoredOrganizationId);
        Task<OrganizationSponsorship> GetByOfferedToEmailAsync(string email);
    }
}
