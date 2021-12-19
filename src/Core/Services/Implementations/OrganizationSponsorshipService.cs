using System;
using System.Threading.Tasks;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.DataProtection;

namespace Bit.Core.Services
{
    public class OrganizationSponsorshipService : IOrganizationSponsorshipService
    {
        private const string FamiliesForEnterpriseTokenName = "FamiliesForEnterpriseToken";
        private const string TokenClearTextPrefix = "BWOrganizationSponsorship_";

        private readonly IOrganizationSponsorshipRepository _organizationSponsorshipRepository;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IUserRepository _userRepository;
        private readonly IPaymentService _paymentService;
        private readonly IMailService _mailService;

        private readonly IDataProtector _dataProtector;

        public OrganizationSponsorshipService(IOrganizationSponsorshipRepository organizationSponsorshipRepository,
            IOrganizationRepository organizationRepository,
            IUserRepository userRepository,
            IPaymentService paymentService,
            IMailService mailService,
            IDataProtectionProvider dataProtectionProvider)
        {
            _organizationSponsorshipRepository = organizationSponsorshipRepository;
            _organizationRepository = organizationRepository;
            _userRepository = userRepository;
            _paymentService = paymentService;
            _mailService = mailService;
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

        private string RedemptionToken(Guid sponsorshipId, PlanSponsorshipType sponsorshipType) =>
            string.Concat(
                TokenClearTextPrefix,
                _dataProtector.Protect($"{FamiliesForEnterpriseTokenName} {sponsorshipId} {sponsorshipType}")
            );

        public async Task OfferSponsorshipAsync(Organization sponsoringOrg, OrganizationUser sponsoringOrgUser,
            PlanSponsorshipType sponsorshipType, string sponsoredEmail, string friendlyName, string sponsoringUserEmail)
        {
            var requiredSponsoringProductType = StaticStore.GetSponsoredPlan(sponsorshipType)?.SponsoringProductType;
            if (requiredSponsoringProductType == null ||
                sponsoringOrg == null ||
                StaticStore.GetPlan(sponsoringOrg.PlanType).Product != requiredSponsoringProductType.Value)
            {
                throw new BadRequestException("Specified Organization cannot sponsor other organizations.");
            }

            if (sponsoringOrgUser == null || sponsoringOrgUser.Status != OrganizationUserStatusType.Confirmed)
            {
                throw new BadRequestException("Only confirmed users can sponsor other organizations.");
            }

            var existingOrgSponsorship = await _organizationSponsorshipRepository
                .GetBySponsoringOrganizationUserIdAsync(sponsoringOrgUser.Id);
            if (existingOrgSponsorship?.SponsoredOrganizationId != null)
            {
                throw new BadRequestException("Can only sponsor one organization per Organization User.");
            }

            var sponsorship = new OrganizationSponsorship
            {
                SponsoringOrganizationId = sponsoringOrg.Id,
                SponsoringOrganizationUserId = sponsoringOrgUser.Id,
                FriendlyName = friendlyName,
                OfferedToEmail = sponsoredEmail,
                PlanSponsorshipType = sponsorshipType,
                CloudSponsor = true,
            };

            if (existingOrgSponsorship != null)
            {
                // Replace existing invalid offer with our new sponsorship offer
                sponsorship.Id = existingOrgSponsorship.Id;
            }

            try
            {
                await _organizationSponsorshipRepository.UpsertAsync(sponsorship);

                await SendSponsorshipOfferAsync(sponsorship, sponsoringUserEmail);
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

        public async Task ResendSponsorshipOfferAsync(Organization sponsoringOrg, OrganizationUser sponsoringOrgUser,
            OrganizationSponsorship sponsorship, string sponsoringUserEmail)
        {
            if (sponsoringOrg == null)
            {
                throw new BadRequestException("Cannot find the requested sponsoring organization.");
            }

            if (sponsoringOrgUser == null || sponsoringOrgUser.Status != OrganizationUserStatusType.Confirmed)
            {
                throw new BadRequestException("Only confirmed users can sponsor other organizations.");
            }

            if (sponsorship == null || sponsorship.OfferedToEmail == null)
            {
                throw new BadRequestException("Cannot find an outstanding sponsorship offer for this organization.");
            }

            await SendSponsorshipOfferAsync(sponsorship, sponsoringUserEmail);
        }

        public async Task SendSponsorshipOfferAsync(OrganizationSponsorship sponsorship, string sponsoringEmail)
        {
            var user = await _userRepository.GetByEmailAsync(sponsorship.OfferedToEmail);
            var isExistingAccount = user != null;

            await _mailService.SendFamiliesForEnterpriseOfferEmailAsync(sponsorship.OfferedToEmail, sponsoringEmail,
                isExistingAccount, RedemptionToken(sponsorship.Id, sponsorship.PlanSponsorshipType.Value));
        }

        public async Task SetUpSponsorshipAsync(OrganizationSponsorship sponsorship,
            Organization sponsoredOrganization)
        {
            if (sponsorship == null)
            {
                throw new BadRequestException("No unredeemed sponsorship offer exists for you.");
            }

            var existingOrgSponsorship = await _organizationSponsorshipRepository
                .GetBySponsoredOrganizationIdAsync(sponsoredOrganization.Id);
            if (existingOrgSponsorship != null)
            {
                throw new BadRequestException("Cannot redeem a sponsorship offer for an organization that is already sponsored. Revoke existing sponsorship first.");
            }

            if (sponsorship.PlanSponsorshipType == null)
            {
                throw new BadRequestException("Cannot set up sponsorship without a known sponsorship type.");
            }

            // Check org to sponsor's product type
            var requiredSponsoredProductType = StaticStore.GetSponsoredPlan(sponsorship.PlanSponsorshipType.Value)?.SponsoredProductType;
            if (requiredSponsoredProductType == null ||
                sponsoredOrganization == null ||
                StaticStore.GetPlan(sponsoredOrganization.PlanType).Product != requiredSponsoredProductType.Value)
            {
                throw new BadRequestException("Can only redeem sponsorship offer on families organizations.");
            }

            await _paymentService.SponsorOrganizationAsync(sponsoredOrganization, sponsorship);
            await _organizationRepository.UpsertAsync(sponsoredOrganization);

            sponsorship.SponsoredOrganizationId = sponsoredOrganization.Id;
            sponsorship.OfferedToEmail = null;
            await _organizationSponsorshipRepository.UpsertAsync(sponsorship);
        }

        public async Task<bool> ValidateSponsorshipAsync(Guid sponsoredOrganizationId)
        {
            var sponsoredOrganization = await _organizationRepository.GetByIdAsync(sponsoredOrganizationId);
            if (sponsoredOrganization == null)
            {
                return false;
            }

            var existingSponsorship = await _organizationSponsorshipRepository
                .GetBySponsoredOrganizationIdAsync(sponsoredOrganizationId);

            if (existingSponsorship == null)
            {
                await DoRemoveSponsorshipAsync(sponsoredOrganization, null);
                return false;
            }

            if (existingSponsorship.SponsoringOrganizationId == null || existingSponsorship.SponsoringOrganizationUserId == null || existingSponsorship.PlanSponsorshipType == null)
            {
                await DoRemoveSponsorshipAsync(sponsoredOrganization, existingSponsorship);
                return false;
            }
            var sponsoredPlan = Utilities.StaticStore.GetSponsoredPlan(existingSponsorship.PlanSponsorshipType.Value);

            var sponsoringOrganization = await _organizationRepository
                .GetByIdAsync(existingSponsorship.SponsoringOrganizationId.Value);
            if (sponsoringOrganization == null)
            {
                await DoRemoveSponsorshipAsync(sponsoredOrganization, existingSponsorship);
                return false;
            }

            var sponsoringOrgPlan = Utilities.StaticStore.GetPlan(sponsoringOrganization.PlanType);
            if (!sponsoringOrganization.Enabled || sponsoredPlan.SponsoringProductType != sponsoringOrgPlan.Product)
            {
                await DoRemoveSponsorshipAsync(sponsoredOrganization, existingSponsorship);
                return false;
            }

            return true;
        }

        public async Task RevokeSponsorshipAsync(Organization sponsoredOrg, OrganizationSponsorship sponsorship)
        {
            if (sponsorship == null)
            {
                throw new BadRequestException("You are not currently sponsoring an organization.");
            }

            if (sponsorship.SponsoredOrganizationId == null)
            {
                await DoRemoveSponsorshipAsync(null, sponsorship);
                return;
            }

            if (sponsoredOrg == null)
            {
                throw new BadRequestException("Unable to find the sponsored Organization.");
            }

            await DoRemoveSponsorshipAsync(sponsoredOrg, sponsorship);
        }

        public async Task RemoveSponsorshipAsync(Organization sponsoredOrg, OrganizationSponsorship sponsorship)
        {
            if (sponsorship == null || sponsorship.SponsoredOrganizationId == null)
            {
                throw new BadRequestException("The requested organization is not currently being sponsored.");
            }

            if (sponsoredOrg == null)
            {
                throw new BadRequestException("Unable to find the sponsored Organization.");
            }

            await DoRemoveSponsorshipAsync(sponsoredOrg, sponsorship);
        }

        internal async Task DoRemoveSponsorshipAsync(Organization sponsoredOrganization, OrganizationSponsorship sponsorship = null)
        {
            if (sponsoredOrganization != null)
            {
                await _paymentService.RemoveOrganizationSponsorshipAsync(sponsoredOrganization, sponsorship);
                await _organizationRepository.UpsertAsync(sponsoredOrganization);

                await _mailService.SendFamiliesForEnterpriseSponsorshipRevertingEmailAsync(
                    sponsoredOrganization.BillingEmailAddress(),
                    sponsoredOrganization.Name);
            }

            if (sponsorship == null)
            {
                return;
            }

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
    }
}
