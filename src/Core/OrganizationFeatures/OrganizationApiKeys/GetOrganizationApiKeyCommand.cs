using System;
using System.Threading.Tasks;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.OrganizationFeatures.OrganizationApiKeys.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Utilities;

namespace Bit.Core.OrganizationFeatures.OrganizationApiKeys
{
    public class GetOrganizationApiKeyCommand : IGetOrganizationApiKeyCommand
    {
        private readonly IOrganizationApiKeyRepository _organizationApiKeyRepository;

        public GetOrganizationApiKeyCommand(IOrganizationApiKeyRepository organizationApiKeyRepository)
        {
            _organizationApiKeyRepository = organizationApiKeyRepository;
        }

        public async Task<OrganizationApiKey> GetOrganizationApiKeyAsync(Guid organizationId, OrganizationApiKeyType? organizationApiKeyType)
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
    }
}
