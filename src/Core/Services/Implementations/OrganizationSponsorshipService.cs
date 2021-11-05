using System;
using System.Threading.Tasks;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
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
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IPaymentService _paymentService;
        private readonly IDataProtector _dataProtector;

        public OrganizationSponsorshipService(IOrganizationSponsorshipRepository organizationSponsorshipRepository,
            IOrganizationRepository organizationRepository,
            IPaymentService paymentService,
            IDataProtector dataProtector)
        {
            _organizationSponsorshipRepository = organizationSponsorshipRepository;
            _organizationRepository = organizationRepository;
            _paymentService = paymentService;
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
            if (sponsorship.PlanSponsorshipType == null)
            {
                throw new BadRequestException("Cannot set up sponsorship without a known sponsorship type.");
            }

            // TODO: rollback?
            await _paymentService.SponsorOrganizationAsync(sponsoredOrganization, sponsorship);
            await _organizationRepository.UpsertAsync(sponsoredOrganization);

            sponsorship.SponsoredOrganizationId = sponsoredOrganization.Id;
            sponsorship.OfferedToEmail = null;
            await _organizationSponsorshipRepository.UpsertAsync(sponsorship);
        }

        public async Task RemoveSponsorshipAsync(OrganizationSponsorship sponsorship, Organization sponsoredOrganization)
        {
            var success = await _paymentService.RemoveOrganizationSponsorshipAsync(sponsoredOrganization, sponsorship);

            if (success)
            {
                if (sponsorship.CloudSponsor || sponsorship.SponsorshipLapsedDate.HasValue)
                {
                    await _organizationSponsorshipRepository.DeleteAsync(sponsorship);
                }
                else
                {
                    sponsorship.SponsoredOrganizationId = null;
                    sponsorship.OfferedToEmail = null;
                    sponsorship.PlanSponsorshipType = null;
                    sponsorship.TimesRenewedWithoutValidation = 0;
                }
            }
            else
            {
                sponsorship.TimesRenewedWithoutValidation += 1;
                sponsorship.SponsorshipLapsedDate ??= DateTime.UtcNow;

                sponsoredOrganization.Enabled = sponsorship.TimesRenewedWithoutValidation <= 6;
            }
            await _organizationSponsorshipRepository.UpsertAsync(sponsorship);
            await _organizationRepository.UpsertAsync(sponsoredOrganization);
        }

    }
}
