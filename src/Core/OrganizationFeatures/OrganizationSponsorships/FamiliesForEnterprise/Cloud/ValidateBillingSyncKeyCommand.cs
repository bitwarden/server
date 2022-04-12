using System;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business.Tokenables;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Tokens;

namespace Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Cloud
{
    public class ValidateBillingSyncKeyCommand : IValidateBillingSyncKeyCommand
    {
        private readonly IOrganizationSponsorshipRepository _organizationSponsorshipRepository;
        private readonly IOrganizationApiKeyRepository _apiKeyRepository;

        public ValidateBillingSyncKeyCommand(
            IOrganizationSponsorshipRepository organizationSponsorshipRepository,
            IOrganizationApiKeyRepository organizationApiKeyRepository)
        {
            _organizationSponsorshipRepository = organizationSponsorshipRepository;
        }

        public async Task<bool> ValidateBillingSyncKeyAsync(Organization organization, string billingSyncKey)
        {
            if (organization == null)
            {
                throw new BadRequestException("Invalid organization");
            }
            if (string.IsNullOrWhiteSpace(billingSyncKey))
            {
                return false;
            }

            var key = (await _apiKeyRepository.GetManyByOrganizationIdTypeAsync(organization.Id, Enums.OrganizationApiKeyType.BillingSync)).FirstOrDefault();
            if (key != null)
            {
                if (billingSyncKey.Equals(key))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
