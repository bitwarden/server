using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Utilities;

namespace Bit.Core.Services
{
    public class OrganizationApiKeyService : IOrganizationApiKeyService
    {
        private readonly IOrganizationApiKeyRepository _organizationApiKeyRepository;

        public OrganizationApiKeyService(IOrganizationApiKeyRepository organizationApiKeyRepository)
        {
            _organizationApiKeyRepository = organizationApiKeyRepository;
        }

        public async Task<OrganizationApiKey> GetOrganizationApiKeyAsync(Guid organizationId, OrganizationApiKeyType? organizationApiKeyType = null)
        {
            var keyType = organizationApiKeyType ?? OrganizationApiKeyType.Default;

            if (!Enum.IsDefined(keyType))
            {
                throw new ArgumentOutOfRangeException(nameof(organizationApiKeyType), $"Invalid value for enum {nameof(OrganizationApiKeyType)}");
            }

            var apiKey = await _organizationApiKeyRepository.GetByOrganizationIdTypeAsync(organizationId, keyType);

            if (apiKey == null)
            {
                apiKey = new OrganizationApiKey
                {
                    OrganizationId = organizationId,
                    Type = keyType,
                    ApiKey = CoreHelpers.SecureRandomString(30),
                    RevisionDate = DateTime.UtcNow
                };

                await _organizationApiKeyRepository.CreateAsync(apiKey);
            }

            return apiKey;
        }

        public async Task<OrganizationApiKey> RotateApiKeyAsync(OrganizationApiKey organizationApiKey)
        {
            organizationApiKey.ApiKey = CoreHelpers.SecureRandomString(30);
            organizationApiKey.RevisionDate = DateTime.UtcNow;
            await _organizationApiKeyRepository.UpdateAsync(organizationApiKey);
            return organizationApiKey;
        }

        public async Task<ICollection<OrganizationApiKey>> GetByOrganizationIdAsync(Guid organizationId)
        {
            return await _organizationApiKeyRepository.GetByOrganizationIdAsync(organizationId);
        }
    }
}
