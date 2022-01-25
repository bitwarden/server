using System;
using System.Threading.Tasks;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.DataProtection;

namespace Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise
{
    public class ValidateRedemptionTokenCommand
    {
        private const string FamiliesForEnterpriseTokenName = "FamiliesForEnterpriseToken";
        private const string TokenClearTextPrefix = "BWOrganizationSponsorship_";

        private readonly IOrganizationSponsorshipRepository _organizationSponsorshipRepository;
        private readonly IDataProtector _dataProtector;

        public ValidateRedemptionTokenCommand(IOrganizationSponsorshipRepository organizationSponsorshipRepository,
        IDataProtectionProvider dataProtectionProvider)
        {
            _organizationSponsorshipRepository = organizationSponsorshipRepository;
            _dataProtector = dataProtectionProvider.CreateProtector("OrganizationSponsorshipServiceDataProtector");
        }

        public async Task<bool> ValidateRedemptionTokenAsync(string encryptedToken, string sponsoredUserEmail)
        {
            if (!encryptedToken.StartsWith(TokenClearTextPrefix) || sponsoredUserEmail == null)
            {
                return false;
            }

            var decryptedToken = _dataProtector.Unprotect(encryptedToken[TokenClearTextPrefix.Length..]);
            var dataParts = decryptedToken.Split(' ');

            if (dataParts.Length != 3)
            {
                return false;
            }

            if (dataParts[0].Equals(FamiliesForEnterpriseTokenName))
            {
                if (!Guid.TryParse(dataParts[1], out Guid sponsorshipId) ||
                    !Enum.TryParse<PlanSponsorshipType>(dataParts[2], true, out var sponsorshipType))
                {
                    return false;
                }

                var sponsorship = await _organizationSponsorshipRepository.GetByIdAsync(sponsorshipId);
                if (sponsorship == null ||
                    sponsorship.PlanSponsorshipType != sponsorshipType ||
                    sponsorship.OfferedToEmail != sponsoredUserEmail)
                {
                    return false;
                }

                return true;
            }

            return false;
        }
    }
}
