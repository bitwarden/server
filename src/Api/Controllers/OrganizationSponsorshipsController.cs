using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Api.Models.Request.Organizations;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Controllers
{
    [Route("organization/sponsorship")]
    public class OrganizationSponsorshipsController : Controller
    {
        private readonly IOrganizationSponsorshipService _organizationsSponsorshipService;
        private readonly IOrganizationSponsorshipRepository _organizationSponsorshipRepository;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly ICurrentContext _currentContext;
        private readonly IOrganizationApiKeyRepository _organizationApiKeyRepository;
        private readonly IUserService _userService;

        public OrganizationSponsorshipsController(IOrganizationSponsorshipService organizationSponsorshipService,
            IOrganizationSponsorshipRepository organizationSponsorshipRepository,
            IOrganizationRepository organizationRepository,
            IOrganizationUserRepository organizationUserRepository,
            IUserService userService,
            IOrganizationApiKeyRepository organizationApiKeyRepository,
            ICurrentContext currentContext)
        {
            _organizationsSponsorshipService = organizationSponsorshipService;
            _organizationSponsorshipRepository = organizationSponsorshipRepository;
            _organizationRepository = organizationRepository;
            _organizationUserRepository = organizationUserRepository;
            _userService = userService;
            _organizationApiKeyRepository = organizationApiKeyRepository;
            _currentContext = currentContext;
        }

        [HttpPost("{sponsoringOrgId}/families-for-enterprise")]
        [SelfHosted(NotSelfHostedOnly = true)]
        [Authorize("Application")]
        public async Task CreateSponsorship(Guid sponsoringOrgId, [FromBody] OrganizationSponsorshipRequestModel model)
        {
            await _organizationsSponsorshipService.OfferSponsorshipAsync(
                await _organizationRepository.GetByIdAsync(sponsoringOrgId),
                await _organizationUserRepository.GetByOrganizationAsync(sponsoringOrgId, _currentContext.UserId ?? default),
                model.PlanSponsorshipType, model.SponsoredEmail, model.FriendlyName,
                (await CurrentUser).Email);
        }

        [HttpPost("{sponsoringOrgId}/families-for-enterprise/resend")]
        [SelfHosted(NotSelfHostedOnly = true)]
        [Authorize("Application")]
        public async Task ResendSponsorshipOffer(Guid sponsoringOrgId)
        {
            var sponsoringOrgUser = await _organizationUserRepository
                .GetByOrganizationAsync(sponsoringOrgId, _currentContext.UserId ?? default);

            await _organizationsSponsorshipService.ResendSponsorshipOfferAsync(
                await _organizationRepository.GetByIdAsync(sponsoringOrgId),
                sponsoringOrgUser,
                await _organizationSponsorshipRepository
                    .GetBySponsoringOrganizationUserIdAsync(sponsoringOrgUser.Id),
                (await CurrentUser).Email);
        }

        [HttpPost("validate-token")]
        [SelfHosted(NotSelfHostedOnly = true)]
        [Authorize("Application")]
        public async Task<bool> PreValidateSponsorshipToken([FromQuery] string sponsorshipToken)
        {
            return await _organizationsSponsorshipService.ValidateRedemptionTokenAsync(sponsorshipToken, (await CurrentUser).Email);
        }

        [HttpPost("redeem")]
        [SelfHosted(NotSelfHostedOnly = true)]
        [Authorize("Application")]
        public async Task RedeemSponsorship([FromQuery] string sponsorshipToken, [FromBody] OrganizationSponsorshipRedeemRequestModel model)
        {
            if (!await _organizationsSponsorshipService.ValidateRedemptionTokenAsync(sponsorshipToken, (await CurrentUser).Email))
            {
                throw new BadRequestException("Failed to parse sponsorship token.");
            }

            if (!await _currentContext.OrganizationOwner(model.SponsoredOrganizationId))
            {
                throw new BadRequestException("Can only redeem sponsorship for an organization you own.");
            }

            await _organizationsSponsorshipService.SetUpSponsorshipAsync(
                await _organizationSponsorshipRepository
                    .GetByOfferedToEmailAsync((await CurrentUser).Email),
                // Check org to sponsor's product type
                await _organizationRepository.GetByIdAsync(model.SponsoredOrganizationId));
        }

        [HttpDelete("{sponsoringOrganizationId}")]
        [HttpPost("{sponsoringOrganizationId}/delete")]
        [SelfHosted(NotSelfHostedOnly = true)]
        [Authorize("Application")]
        public async Task RevokeSponsorship(Guid sponsoringOrganizationId)
        {

            var orgUser = await _organizationUserRepository.GetByOrganizationAsync(sponsoringOrganizationId, _currentContext.UserId ?? default);
            if (_currentContext.UserId != orgUser?.UserId)
            {
                throw new BadRequestException("Can only revoke a sponsorship you granted.");
            }

            var existingOrgSponsorship = await _organizationSponsorshipRepository
                .GetBySponsoringOrganizationUserIdAsync(orgUser.Id);

            await _organizationsSponsorshipService.RevokeSponsorshipAsync(
                await _organizationRepository
                    .GetByIdAsync(existingOrgSponsorship.SponsoredOrganizationId ?? default),
                existingOrgSponsorship);
        }

        [HttpDelete("sponsored/{sponsoredOrgId}")]
        [HttpPost("sponsored/{sponsoredOrgId}/remove")]
        [SelfHosted(NotSelfHostedOnly = true)]
        [Authorize("Application")]
        public async Task RemoveSponsorship(Guid sponsoredOrgId)
        {

            if (!await _currentContext.OrganizationOwner(sponsoredOrgId))
            {
                throw new BadRequestException("Only the owner of an organization can remove sponsorship.");
            }

            var existingOrgSponsorship = await _organizationSponsorshipRepository
                .GetBySponsoredOrganizationIdAsync(sponsoredOrgId);

            await _organizationsSponsorshipService.RemoveSponsorshipAsync(
                await _organizationRepository
                    .GetByIdAsync(existingOrgSponsorship.SponsoredOrganizationId.Value),
                existingOrgSponsorship);
        }

        [HttpPost("sync")]
        [Authorize("SyncBilling")]
        [SelfHosted(NotSelfHostedOnly = true)]
        public async Task<IActionResult> SyncSponsorships([FromBody] SyncOrganizationSponsorshipsRequestModel syncModel, [FromQuery] string key)
        {
            if (!await _organizationApiKeyRepository.GetCanUseByApiKeyAsync(syncModel.OrganizationId, key, OrganizationApiKeyType.BillingSync))
            {
                return Unauthorized();
            }

            await Task.Delay(1000);
            return Ok(new { Message = "Hi", Key = key, Echo = syncModel});
        }

        private Task<User> CurrentUser => _userService.GetUserByIdAsync(_currentContext.UserId.Value);
    }
}
