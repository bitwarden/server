using System;
using System.Threading.Tasks;
using Bit.Core.Enums;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.DataProtection;

namespace Bit.Core.Services
{
    public class OrganizationSponsorshipService : IOrganizationSponsorshipService
    {
        private const string FamiliesForEnterpriseTokenName = "FamiliesForEnterpriseToken";
        private const string TokenClearTextPrefix = "BWOrganizationSponsorship_";

        private readonly IOrganizationSponsorshipRepository _organizationSponsorshipRepository;
        private readonly IDataProtector _dataProtector;

        public OrganizationSponsorshipService(IOrganizationSponsorshipRepository organizationSponsorshipRepository,
            IDataProtector dataProtector)
        {
            _organizationSponsorshipRepository = organizationSponsorshipRepository;
            _dataProtector = dataProtector;
        }

        public async Task<bool> ValidateRedemptionTokenAsync(string encryptedToken)
        {
            if (!encryptedToken.StartsWith(TokenClearTextPrefix))
            {
                return false;
            }

            var decryptedToken = _dataProtector.Unprotect(encryptedToken);
            var dataParts = decryptedToken.Split(' ');

            if (dataParts.Length != 2)
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
                if (sponsorship == null || sponsorship.PlanSponsorshipType != sponsorshipType)
                {
                    return false;
                }

                return true;
            }

            return false;
        }

        private string RedemptionToken(Guid sponsorshipId, PlanSponsorshipType sponsorshipType) =>
            string.Concat(
                TokenClearTextPrefix,
                _dataProtector.Protect($"{FamiliesForEnterpriseTokenName} {sponsorshipId} {sponsorshipType}")
            );

        public async Task OfferSponsorshipAsync(Organization sponsoringOrg, OrganizationUser sponsoringOrgUser, PlanSponsorshipType sponsorshipType, string sponsoredEmail)
        {
            var sponsorship = new OrganizationSponsorship
            {
                SponsoringOrganizationId = sponsoringOrg.Id,
                SponsoringOrganizationUserId = sponsoringOrgUser.Id,
                OfferedToEmail = sponsoredEmail,
                PlanSponsorshipType = sponsorshipType,
                CloudSponsor = true,
            };

            try
            {
                sponsorship = await _organizationSponsorshipRepository.CreateAsync(sponsorship);

                // TODO: send email to sponsoredEmail w/ redemption token link
                var _ = RedemptionToken(sponsorship.Id, sponsorshipType);
            }
            catch
            {
                if (sponsorship.Id != default)
                {
                    await _organizationSponsorshipRepository.DeleteAsync(sponsorship);
                }
                throw;
            }
        }

        public async Task SetUpSponsorshipAsync(OrganizationSponsorship sponsorship, Organization sponsoredOrganization)
        {
            // TODO: set up sponsorship, remember remove offeredToEmail from sponsorship
            throw new NotImplementedException();
        }

        public async Task RemoveSponsorshipAsync(OrganizationSponsorship sponsorship)
        {
            // TODO: remove sponsorship
            throw new NotImplementedException();
        }

    }
}
