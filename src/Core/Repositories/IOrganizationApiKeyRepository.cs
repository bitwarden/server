using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Entities;

namespace Bit.Core.Repositories
{
    public interface IOrganizationApiKeyRepository : IRepository<OrganizationApiKey, Guid>
    {
        Task<ICollection<OrganizationApiKey>> GetByOrganizationIdAsync(Guid organizationId);
        Task<bool> GetCanUseByApiKeyAsync(Guid organizationId, string apiKey);
    }
}
