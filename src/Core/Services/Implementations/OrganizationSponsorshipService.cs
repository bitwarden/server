using System;
using System.Threading.Tasks;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Utilities;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.DataProtection;
using Bit.Core.Context;
using Bit.Core.Models.Api;

namespace Bit.Core.Services
{
    public class OrganizationSponsorshipService : IOrganizationSponsorshipService
    {
        private const string FamiliesForEnterpriseTokenName = "FamiliesForEnterpriseToken";
        private const string TokenClearTextPrefix = "BWOrganizationSponsorship_";

        private readonly IOrganizationSponsorshipRepository _organizationSponsorshipRepository;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IUserRepository _userRepository;
        private readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly IPaymentService _paymentService;
        private readonly IMailService _mailService;
        private readonly IUserService _userService;
        private readonly ICurrentContext _currentContext;

        private readonly IDataProtector _dataProtector;

        public OrganizationSponsorshipService(IOrganizationSponsorshipRepository organizationSponsorshipRepository,
            IOrganizationRepository organizationRepository,
            IUserRepository userRepository,
            IOrganizationUserRepository organizationUserRepository,
            IPaymentService paymentService,
            IMailService mailService,
            IUserService userService,
            ICurrentContext currentContext,
            IDataProtectionProvider dataProtectionProvider)
        {
            _organizationSponsorshipRepository = organizationSponsorshipRepository;
            _organizationRepository = organizationRepository;
            _organizationUserRepository = organizationUserRepository;
            _userRepository = userRepository;
            _paymentService = paymentService;
            _mailService = mailService;
            _userService = userService;
            _currentContext = currentContext;
            _dataProtector = dataProtectionProvider.CreateProtector("OrganizationSponsorshipServiceDataProtector");
        }

        internal async Task<bool> ValidateRedemptionTokenAsync(string encryptedToken)
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
                if (!Guid.TryParse(dataParts[1], out var sponsorshipId) ||
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

        public async Task RevokeSponsorshipAsync(Guid sponsoringOrganizationId)
        {
            var orgUser = await _organizationUserRepository.GetByOrganizationAsync(sponsoringOrganizationId, _currentContext.UserId ?? default);
            if (_currentContext.UserId != orgUser?.UserId)
            {
                throw new BadRequestException("Can only revoke a sponsorship you granted.");
            }

            var existingOrgSponsorship = await _organizationSponsorshipRepository
                .GetBySponsoringOrganizationUserIdAsync(orgUser.Id);
            if (existingOrgSponsorship == null)
            {
                throw new BadRequestException("You are not currently sponsoring an organization.");
            }
            if (existingOrgSponsorship.SponsoredOrganizationId == null)
            {
                await _organizationSponsorshipRepository.DeleteAsync(existingOrgSponsorship);
                return;
            }

            var sponsoredOrganization = await _organizationRepository
                .GetByIdAsync(existingOrgSponsorship.SponsoredOrganizationId.Value);
            if (sponsoredOrganization == null)
            {
                throw new BadRequestException("Unable to find the sponsored Organization.");
            }

            await RemoveSponsorshipAsync(sponsoredOrganization, existingOrgSponsorship);
        }

        public async Task CreateSponsorshipAsync(Guid sponsoringOrgId, OrganizationSponsorshipRequestModel model)
        {
            var requiredSponsoringProductType = StaticStore.GetSponsoredPlan(model.PlanSponsorshipType)?.SponsoringProductType;
            var sponsoringOrg = await _organizationRepository.GetByIdAsync(sponsoringOrgId);
            if (requiredSponsoringProductType == null ||
                sponsoringOrg == null ||
                StaticStore.GetPlan(sponsoringOrg.PlanType).Product != requiredSponsoringProductType.Value)
            {
                throw new BadRequestException("Specified Organization cannot sponsor other organizations.");
            }

            var sponsoringOrgUser = await _organizationUserRepository.GetByOrganizationAsync(sponsoringOrgId, _currentContext.UserId ?? default);
            if (sponsoringOrgUser == null || sponsoringOrgUser.Status != OrganizationUserStatusType.Confirmed)
            {
                throw new BadRequestException("Only confirmed users can sponsor other organizations.");
            }

            var existingOrgSponsorship = await _organizationSponsorshipRepository.GetBySponsoringOrganizationUserIdAsync(sponsoringOrgUser.Id);
            if (existingOrgSponsorship != null)
            {
                throw new BadRequestException("Can only sponsor one organization per Organization User.");
            }

            await OfferSponsorshipAsync(sponsoringOrg, sponsoringOrgUser,
                model.PlanSponsorshipType, model.SponsoredEmail, model.FriendlyName);
        }

        public async Task RedeemSponsorshipAsync(string sponsorshipToken, OrganizationSponsorshipRedeemRequestModel model)
        {
            if (!await ValidateRedemptionTokenAsync(sponsorshipToken))
            {
                throw new BadRequestException("Failed to parse sponsorship token.");
            }

            if (!await _currentContext.OrganizationOwner(model.SponsoredOrganizationId))
            {
                throw new BadRequestException("Can only redeem sponsorship for an organization you own.");
            }
            var existingSponsorshipOffer = await _organizationSponsorshipRepository
                .GetByOfferedToEmailAsync((await CurrentUser).Email);
            if (existingSponsorshipOffer == null)
            {
                throw new BadRequestException("No unredeemed sponsorship offer exists for you.");
            }
            if ((await CurrentUser).Email != existingSponsorshipOffer.OfferedToEmail)
            {
                throw new BadRequestException("This sponsorship offer was issued to a different user email address.");
            }

            var existingOrgSponsorship = await _organizationSponsorshipRepository
                .GetBySponsoredOrganizationIdAsync(model.SponsoredOrganizationId);
            if (existingOrgSponsorship != null)
            {
                throw new BadRequestException("Cannot redeem a sponsorship offer for an organization that is already sponsored. Revoke existing sponsorship first.");
            }

            // Check org to sponsor's product type
            var requiredSponsoredProductType = StaticStore.GetSponsoredPlan(model.PlanSponsorshipType)?.SponsoredProductType;
            var organizationToSponsor = await _organizationRepository.GetByIdAsync(model.SponsoredOrganizationId);
            if (requiredSponsoredProductType == null ||
                organizationToSponsor == null ||
                StaticStore.GetPlan(organizationToSponsor.PlanType).Product != requiredSponsoredProductType.Value)
            {
                throw new BadRequestException("Can only redeem sponsorship offer on families organizations.");
            }

            await SetUpSponsorshipAsync(existingSponsorshipOffer, organizationToSponsor);
        }

        private Task<User> CurrentUser
            => _userService.GetUserByIdAsync(_currentContext.UserId.Value);

        private string RedemptionToken(Guid sponsorshipId, PlanSponsorshipType sponsorshipType) =>
            string.Concat(
                TokenClearTextPrefix,
                _dataProtector.Protect($"{FamiliesForEnterpriseTokenName} {sponsorshipId} {sponsorshipType}")
            );

        internal async Task OfferSponsorshipAsync(Organization sponsoringOrg, OrganizationUser sponsoringOrgUser,
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

                await SendSponsorshipOfferAsync(sponsoringOrg, sponsorship);
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

        public async Task ResendSponsorshipOfferAsync(Guid sponsoringOrgId)
        {
            var sponsoringOrg = await _organizationRepository.GetByIdAsync(sponsoringOrgId);
            if (sponsoringOrg == null)
            {
                throw new BadRequestException("Cannot find the requested sponsoring organization.");
            }

            var sponsoringOrgUser = await _organizationUserRepository.GetByOrganizationAsync(sponsoringOrgId, _currentContext.UserId ?? default);
            if (sponsoringOrgUser == null || sponsoringOrgUser.Status != OrganizationUserStatusType.Confirmed)
            {
                throw new BadRequestException("Only confirmed users can sponsor other organizations.");
            }

            var existingOrgSponsorship = await _organizationSponsorshipRepository.GetBySponsoringOrganizationUserIdAsync(sponsoringOrgUser.Id);
            if (existingOrgSponsorship == null || existingOrgSponsorship.OfferedToEmail == null)
            {
                throw new BadRequestException("Cannot find an outstanding sponsorship offer for this organization.");
            }

            await SendSponsorshipOfferAsync(sponsoringOrg, existingOrgSponsorship);
        }

        internal async Task SendSponsorshipOfferAsync(Organization sponsoringOrg, OrganizationSponsorship sponsorship)
        {
            var user = await _userRepository.GetByEmailAsync(sponsorship.OfferedToEmail);
            var isExistingAccount = user != null;

            await _mailService.SendFamiliesForEnterpriseOfferEmailAsync(sponsorship.OfferedToEmail, sponsoringOrg.Name,
                isExistingAccount, RedemptionToken(sponsorship.Id, sponsorship.PlanSponsorshipType.Value));
        }

        internal async Task SetUpSponsorshipAsync(OrganizationSponsorship sponsorship, Organization sponsoredOrganization)
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
            if (sponsoredOrganization == null)
            {
                return false;
            }

            var existingSponsorship = await _organizationSponsorshipRepository
                .GetBySponsoredOrganizationIdAsync(sponsoredOrganizationId);

            if (existingSponsorship == null)
            {
                await RemoveSponsorshipAsync(sponsoredOrganization, null);
                return false;
            }

            if (existingSponsorship.SponsoringOrganizationId == null || existingSponsorship.SponsoringOrganizationUserId == null || existingSponsorship.PlanSponsorshipType == null)
            {
                await RemoveSponsorshipAsync(sponsoredOrganization, existingSponsorship);
                return false;
            }
            var sponsoredPlan = Utilities.StaticStore.GetSponsoredPlan(existingSponsorship.PlanSponsorshipType.Value);

            var sponsoringOrganization = await _organizationRepository
                .GetByIdAsync(existingSponsorship.SponsoringOrganizationId.Value);
            if (sponsoringOrganization == null)
            {
                await RemoveSponsorshipAsync(sponsoredOrganization, existingSponsorship);
                return false;
            }

            var sponsoringOrgPlan = Utilities.StaticStore.GetPlan(sponsoringOrganization.PlanType);
            if (!sponsoringOrganization.Enabled || sponsoredPlan.SponsoringProductType != sponsoringOrgPlan.Product)
            {
                await RemoveSponsorshipAsync(sponsoredOrganization, existingSponsorship);
                return false;
            }

            return true;
        }

        public async Task RemoveSponsorshipAsync(Guid sponsoredOrgId)
        {
            if (!await _currentContext.OrganizationOwner(sponsoredOrgId))
            {
                throw new BadRequestException("Only the owner of an organization can remove sponsorship.");
            }

            var existingOrgSponsorship = await _organizationSponsorshipRepository
                .GetBySponsoredOrganizationIdAsync(sponsoredOrgId);
            if (existingOrgSponsorship == null || existingOrgSponsorship.SponsoredOrganizationId == null)
            {
                throw new BadRequestException("The requested organization is not currently being sponsored.");
            }

            var sponsoredOrganization = await _organizationRepository
                .GetByIdAsync(existingOrgSponsorship.SponsoredOrganizationId.Value);
            if (sponsoredOrganization == null)
            {
                throw new BadRequestException("Unable to find the sponsored Organization.");
            }


            await RemoveSponsorshipAsync(sponsoredOrganization, existingOrgSponsorship);
        }

        internal async Task RemoveSponsorshipAsync(Organization sponsoredOrganization, OrganizationSponsorship sponsorship = null)
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
