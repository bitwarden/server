using System;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Api;
using Bit.Core.Models.Api.Request;
using Bit.Core.Models.Table;
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
        private readonly IUserService _userService;

        public OrganizationSponsorshipsController(IOrganizationSponsorshipService organizationSponsorshipService,
            IOrganizationSponsorshipRepository organizationSponsorshipRepository,
            IOrganizationRepository organizationRepository,
            IOrganizationUserRepository organizationUserRepository,
            IUserService userService,
            ICurrentContext currentContext)
        {
            _organizationsSponsorshipService = organizationSponsorshipService;
            _organizationSponsorshipRepository = organizationSponsorshipRepository;
            _organizationRepository = organizationRepository;
            _organizationUserRepository = organizationUserRepository;
            _userService = userService;
            _currentContext = currentContext;
        }

        [HttpPost("{sponsoringOrgId}/families-for-enterprise")]
        [SelfHosted(NotSelfHostedOnly = true)]
        public async Task CreateSponsorship(Guid sponsoringOrgId, [FromBody] OrganizationSponsorshipRequestModel model)
        {
            var sponsoringOrg = await _organizationRepository.GetByIdAsync(sponsoringOrgId);

            var sponsoringOrgUser = await _organizationUserRepository.GetByOrganizationAsync(sponsoringOrgId, _currentContext.UserId ?? default);

            await _organizationsSponsorshipService.OfferSponsorshipAsync(sponsoringOrg, sponsoringOrgUser,
                model.PlanSponsorshipType, model.SponsoredEmail, model.FriendlyName);
        }

        [HttpPost("{sponsoringOrgId}/families-for-enterprise/resend")]
        [SelfHosted(NotSelfHostedOnly = true)]
        public async Task ResendSponsorshipOffer(Guid sponsoringOrgId)
        {
            var sponsoringOrg = await _organizationRepository.GetByIdAsync(sponsoringOrgId);
            var sponsoringOrgUser = await _organizationUserRepository
                .GetByOrganizationAsync(sponsoringOrgId, _currentContext.UserId ?? default);
            var existingOrgSponsorship = await _organizationSponsorshipRepository
                .GetBySponsoringOrganizationUserIdAsync(sponsoringOrgUser.Id);

            await _organizationsSponsorshipService.ResendSponsorshipOfferAsync(sponsoringOrg, sponsoringOrgUser,
                existingOrgSponsorship);
        }

        [HttpPost("redeem")]
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
                .GetByOfferedToEmailAsync((await CurrentUser).Email);

            // Check org to sponsor's product type
            var organizationToSponsor = await _organizationRepository.GetByIdAsync(model.SponsoredOrganizationId);

            await _organizationsSponsorshipService.SetUpSponsorshipAsync(existingSponsorshipOffer, organizationToSponsor);
        }

        [HttpDelete("{sponsoringOrganizationId}")]
        [HttpPost("{sponsoringOrganizationId}/delete")]
        [SelfHosted(NotSelfHostedOnly = true)]
        public async Task RevokeSponsorship(Guid sponsoringOrganizationId)
        {

            var orgUser = await _organizationUserRepository.GetByOrganizationAsync(sponsoringOrganizationId, _currentContext.UserId ?? default);
            if (_currentContext.UserId != orgUser?.UserId)
            {
                throw new BadRequestException("Can only revoke a sponsorship you granted.");
            }

            var existingOrgSponsorship = await _organizationSponsorshipRepository
                .GetBySponsoringOrganizationUserIdAsync(orgUser.Id);

            var sponsoredOrganization = await _organizationRepository
                .GetByIdAsync(existingOrgSponsorship.SponsoredOrganizationId.Value);

            await _organizationsSponsorshipService.RevokeSponsorshipAsync(sponsoredOrganization, existingOrgSponsorship);
        }

        [HttpDelete("sponsored/{sponsoredOrgId}")]
        [HttpPost("sponsored/{sponsoredOrgId}/remove")]
        [SelfHosted(NotSelfHostedOnly = true)]
        public async Task RemoveSponsorship(Guid sponsoredOrgId)
        {

            if (!await _currentContext.OrganizationOwner(sponsoredOrgId))
            {
                throw new BadRequestException("Only the owner of an organization can remove sponsorship.");
            }

            var existingOrgSponsorship = await _organizationSponsorshipRepository
                .GetBySponsoredOrganizationIdAsync(sponsoredOrgId);

            var sponsoredOrganization = await _organizationRepository
                .GetByIdAsync(existingOrgSponsorship.SponsoredOrganizationId.Value);

            await _organizationsSponsorshipService.RemoveSponsorshipAsync(sponsoredOrganization, existingOrgSponsorship);
        }

        private Task<User> CurrentUser => _userService.GetUserByIdAsync(_currentContext.UserId.Value);
    }
}
