using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Entities;
using Bit.Core.Enums;

namespace Bit.Core.Repositories
{
    public interface IOrganizationApiKeyRepository : IRepository<OrganizationApiKey, Guid>
    {
        Task<ICollection<OrganizationApiKey>> GetManyByOrganizationIdAsync(Guid organizationId);
        Task<OrganizationApiKey> GetByOrganizationIdTypeAsync(Guid organizationId, OrganizationApiKeyType type);
    }
}
