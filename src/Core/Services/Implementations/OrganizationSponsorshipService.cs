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
        private readonly IMailService _mailService;

        private readonly IDataProtector _dataProtector;

        public OrganizationSponsorshipService(IOrganizationSponsorshipRepository organizationSponsorshipRepository,
            IOrganizationRepository organizationRepository,
            IPaymentService paymentService,
            IMailService mailService,
            IDataProtectionProvider dataProtectionProvider)
        {
            _organizationSponsorshipRepository = organizationSponsorshipRepository;
            _organizationRepository = organizationRepository;
            _paymentService = paymentService;
            _mailService = mailService;
            _dataProtector = dataProtectionProvider.CreateProtector("OrganizationSponsorshipServiceDataProtector");
        }

        public async Task<bool> ValidateRedemptionTokenAsync(string encryptedToken)
        {
            if (!encryptedToken.StartsWith(TokenClearTextPrefix))
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

        public async Task OfferSponsorshipAsync(Organization sponsoringOrg, OrganizationUser sponsoringOrgUser,
            PlanSponsorshipType sponsorshipType, string sponsoredEmail, string friendlyName)
        {
            var sponsorship = new OrganizationSponsorship
            {
                SponsoringOrganizationId = sponsoringOrg.Id,
                SponsoringOrganizationUserId = sponsoringOrgUser.Id,
                FriendlyName = friendlyName,
                OfferedToEmail = sponsoredEmail,
                PlanSponsorshipType = sponsorshipType,
                CloudSponsor = true,
            };

            try
            {
                sponsorship = await _organizationSponsorshipRepository.CreateAsync(sponsorship);

                await _mailService.SendFamiliesForEnterpriseOfferEmailAsync(sponsoredEmail, sponsoringOrg.Name,
                    RedemptionToken(sponsorship.Id, sponsorshipType));
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

        public async Task<bool> ValidateSponsorshipAsync(Guid sponsoredOrganizationId)
        {
            var sponsoredOrganization = await _organizationRepository.GetByIdAsync(sponsoredOrganizationId);
            var existingSponsorship = await _organizationSponsorshipRepository
                .GetBySponsoredOrganizationIdAsync(sponsoredOrganizationId);

            if (existingSponsorship == null)
            {
                await RemoveSponsorshipAsync(sponsoredOrganization);
                // TODO on fail, mark org as disabled.
                return false;
            }

            var validated = true;
            if (existingSponsorship.SponsoringOrganizationId == null || existingSponsorship.SponsoringOrganizationUserId == null)
            {
                await RemoveSponsorshipAsync(sponsoredOrganization);
                validated = false;
            }

            var sponsoringOrganization = await _organizationRepository
                .GetByIdAsync(existingSponsorship.SponsoringOrganizationId.Value);
            if (!sponsoringOrganization.Enabled)
            {
                await RemoveSponsorshipAsync(sponsoredOrganization);
                validated = false;
            }

            if (!validated && existingSponsorship.SponsoredOrganizationId != null)
            {
                existingSponsorship.TimesRenewedWithoutValidation += 1;
                existingSponsorship.SponsorshipLapsedDate ??= DateTime.UtcNow;

                await _organizationSponsorshipRepository.UpsertAsync(existingSponsorship);
                if (existingSponsorship.TimesRenewedWithoutValidation >= 6)
                {
                    sponsoredOrganization.Enabled = false;
                    await _organizationRepository.UpsertAsync(sponsoredOrganization);
                }
            }

            return true;
        }

        public async Task RemoveSponsorshipAsync(Organization sponsoredOrganization, OrganizationSponsorship sponsorship = null)
        {
            var success = await _paymentService.RemoveOrganizationSponsorshipAsync(sponsoredOrganization);
            await _organizationRepository.UpsertAsync(sponsoredOrganization);

            if (sponsorship == null)
            {
                return;
            }

            if (success)
            {
                // Initialize the record as available
                sponsorship.SponsoredOrganizationId = null;
                sponsorship.FriendlyName = null;
                sponsorship.OfferedToEmail = null;
                sponsorship.PlanSponsorshipType = null;
                sponsorship.TimesRenewedWithoutValidation = 0;
                sponsorship.SponsorshipLapsedDate = null;

                if (sponsorship.CloudSponsor || sponsorship.SponsorshipLapsedDate.HasValue)
                {
                    await _organizationSponsorshipRepository.DeleteAsync(sponsorship);
                }
                else
                {
                    await _organizationSponsorshipRepository.UpsertAsync(sponsorship);
                }
            }
            else
            {
                sponsorship.SponsoringOrganizationId = null;
                sponsorship.SponsoringOrganizationUserId = null;

                if (!sponsorship.CloudSponsor)
                {
                    // Sef-hosted sponsorship record
                    // we need to make the existing sponsorship available, and add
                    // a new sponsorship record to record the lapsed sponsorship
                    var cleanSponsorship = new OrganizationSponsorship
                    {
                        InstallationId = sponsorship.InstallationId,
                        SponsoringOrganizationId = sponsorship.SponsoringOrganizationId,
                        SponsoringOrganizationUserId = sponsorship.SponsoringOrganizationUserId,
                        CloudSponsor = sponsorship.CloudSponsor,
                    };
                    await _organizationSponsorshipRepository.UpsertAsync(cleanSponsorship);
                }

                sponsorship.SponsorshipLapsedDate ??= DateTime.UtcNow;
                await _organizationSponsorshipRepository.UpsertAsync(sponsorship);
            }

        }

    }
}
