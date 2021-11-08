using System;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Api;
using Bit.Core.Models.Api.Request;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Controllers
{
    [Route("organization/sponsorship")]
    [Authorize("Application")]
    public class OrganizationSponsorshipsController : Controller
    {
        private readonly IOrganizationSponsorshipService _organizationsSponsorshipService;
        private readonly IOrganizationSponsorshipRepository _organizationSponsorshipRepository;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly ICurrentContext _currentContext;
        public OrganizationSponsorshipsController(IOrganizationSponsorshipService organizationSponsorshipService,
            IOrganizationSponsorshipRepository organizationSponsorshipRepository,
            IOrganizationRepository organizationRepository,
            IOrganizationUserRepository organizationUserRepository,
            ICurrentContext currentContext)
        {
            _organizationsSponsorshipService = organizationSponsorshipService;
            _organizationSponsorshipRepository = organizationSponsorshipRepository;
            _organizationRepository = organizationRepository;
            _organizationUserRepository = organizationUserRepository;
            _currentContext = currentContext;
        }

        [HttpPost("{sponsoringOrgId}/families-for-enterprise")]
        [SelfHosted(NotSelfHostedOnly = true)]
        public async Task CreateSponsorship(string sponsoringOrgId, [FromBody] OrganizationSponsorshipRequestModel model)
        {
            // TODO: validate has right to sponsor, send sponsorship email
            var sponsoringOrgIdGuid = new Guid(sponsoringOrgId);
            var requiredSponsoringProductType = StaticStore.GetSponsoredPlan(model.PlanSponsorshipType)?.SponsoringProductType;
            var sponsoringOrg = await _organizationRepository.GetByIdAsync(sponsoringOrgIdGuid);
            if (requiredSponsoringProductType == null ||
                sponsoringOrg == null ||
                StaticStore.GetPlan(sponsoringOrg.PlanType).Product != requiredSponsoringProductType.Value)
            {
                throw new BadRequestException("Specified Organization cannot sponsor other organizations.");
            }

            var sponsoringOrgUser = await _organizationUserRepository.GetByIdAsync(model.OrganizationUserId);
            if (sponsoringOrgUser == null || sponsoringOrgUser.Status != OrganizationUserStatusType.Confirmed)
            {
                throw new BadRequestException("Only confirm users can sponsor other organizations.");
            }
            if (sponsoringOrgUser.UserId != _currentContext.UserId)
            {
                throw new BadRequestException("Can only create organization sponsorships for yourself.");
            }

            var existingOrgSponsorship = await _organizationSponsorshipRepository.GetBySponsoringOrganizationUserIdAsync(sponsoringOrgUser.Id);
            if (existingOrgSponsorship != null)
            {
                throw new BadRequestException("Can only sponsor one organization per Organization User.");
            }

            await _organizationsSponsorshipService.OfferSponsorshipAsync(sponsoringOrg, sponsoringOrgUser,
                model.PlanSponsorshipType, model.SponsoredEmail, model.FriendlyName);
        }

        [HttpPost("sponsored/redeem/families-for-enterprise")]
        [SelfHosted(NotSelfHostedOnly = true)]
        public async Task RedeemSponsorship([FromQuery] string sponsorshipToken, [FromBody] OrganizationSponsorshipRedeemRequestModel model)
        {
            if (!await _organizationsSponsorshipService.ValidateRedemptionTokenAsync(sponsorshipToken))
            {
                throw new BadRequestException("Failed to parse sponsorship token.");
            }

            if (!await _currentContext.OrganizationOwner(model.SponsoredOrganizationId))
            {
                throw new BadRequestException("Can only redeem sponsorship for an organization you own.");
            }
            var existingSponsorshipOffer = await _organizationSponsorshipRepository
                .GetByOfferedToEmailAsync(_currentContext.User.Email);
            if (existingSponsorshipOffer == null)
            {
                throw new BadRequestException("No unredeemed sponsorship offer exists for you.");
            }
            if (_currentContext.User.Email != existingSponsorshipOffer.OfferedToEmail)
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

            await _organizationsSponsorshipService.SetUpSponsorshipAsync(existingSponsorshipOffer, organizationToSponsor);
        }

        [HttpDelete("{sponsoringOrgUserId}")]
        [HttpPost("{sponsoringOrgUserId}/delete")]
        [SelfHosted(NotSelfHostedOnly = true)]
        public async Task RevokeSponsorship(string sponsoringOrgUserId)
        {
            var sponsoringOrgUserIdGuid = new Guid(sponsoringOrgUserId);

            var orgUser = await _organizationUserRepository.GetByIdAsync(sponsoringOrgUserIdGuid);
            if (_currentContext.UserId != orgUser?.UserId)
            {
                throw new BadRequestException("Can only revoke a sponsorship you granted.");
            }

            var existingOrgSponsorship = await _organizationSponsorshipRepository
                .GetBySponsoringOrganizationUserIdAsync(sponsoringOrgUserIdGuid);
            if (existingOrgSponsorship == null || existingOrgSponsorship.SponsoredOrganizationId == null)
            {
                throw new BadRequestException("You are not currently sponsoring an organization.");
            }

            var sponsoredOrganization = await _organizationRepository
                .GetByIdAsync(existingOrgSponsorship.SponsoredOrganizationId.Value);
            if (sponsoredOrganization == null)
            {
                throw new BadRequestException("Unable to find the sponsored Organization.");
            }

            await _organizationsSponsorshipService.RemoveSponsorshipAsync(sponsoredOrganization, existingOrgSponsorship);
        }

        [HttpDelete("sponsored/{sponsoredOrgId}")]
        [HttpPost("sponsored/{sponsoredOrgId}/remove")]
        [SelfHosted(NotSelfHostedOnly = true)]
        public async Task RemoveSponsorship(string sponsoredOrgId)
        {
            var sponsoredOrgIdGuid = new Guid(sponsoredOrgId);

            if (!await _currentContext.OrganizationOwner(sponsoredOrgIdGuid))
            {
                throw new BadRequestException("Only the owner of an organization can remove sponsorship.");
            }

            var existingOrgSponsorship = await _organizationSponsorshipRepository
                .GetBySponsoredOrganizationIdAsync(sponsoredOrgIdGuid);
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


            await _organizationsSponsorshipService.RemoveSponsorshipAsync(sponsoredOrganization, existingOrgSponsorship);
        }
    }
}
