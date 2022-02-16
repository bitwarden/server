using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Entities;
using Bit.Core.Enums;

namespace Bit.Core.Repositories
{
    public interface IOrganizationApiKeyRepository
    {
        Task<ICollection<OrganizationApiKey>> GetByOrganizationIdAsync(Guid organizationId);
        Task<bool> GetCanUseByApiKeyAsync(Guid organizationId, string apiKey, OrganizationApiKeyType type);
        Task<OrganizationApiKey> GetByOrganizationIdTypeAsync(Guid organizationId, OrganizationApiKeyType type);
        Task CreateAsync(OrganizationApiKey organizationApiKey);
        Task UpdateAsync(OrganizationApiKey organizationApiKey);
    }
}
