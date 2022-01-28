using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Entities;
using Bit.Core.Enums;

namespace Bit.Core.Services
{
    public interface IOrganizationApiKeyService
    {
        Task<ICollection<OrganizationApiKey>> GetByOrganizationIdAsync(Guid organizationId);
        Task<OrganizationApiKey> GetOrganizationApiKeyAsync(Guid organizationId, OrganizationApiKeyType? organizationApiKeyType = null);
        Task<OrganizationApiKey> RotateApiKeyAsync(OrganizationApiKey organizationApiKey);
    }
}
