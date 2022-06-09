using System;
using System.Threading.Tasks;
using Bit.Core.Entities;
using Bit.Core.Enums;

namespace Bit.Core.OrganizationFeatures.OrganizationApiKeys.Interfaces
{
    public interface IGetOrganizationApiKeyCommand
    {
        Task<OrganizationApiKey> GetOrganizationApiKeyAsync(Guid organizationId, OrganizationApiKeyType organizationApiKeyType);
    }
}
