using System;
using System.Threading.Tasks;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Api;
using Bit.Core.Models.Api.Request;
using Bit.Core.Repositories;
using Bit.Core.Services;
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
        public async Task<IActionResult> CreateSponsorship(string sponsoringOrgId, [FromBody] OrganizationSponsorshipRequestModel model)
        {
            // TODO: validate has right to sponsor, send sponsorship email
            var sponsoringOrgIdGuid = new Guid(sponsoringOrgId);
            var sponsoringOrg = await _organizationRepository.GetByIdAsync(sponsoringOrgIdGuid);
            if (sponsoringOrg == null || !PlanTypeHelper.HasEnterprisePlan(sponsoringOrg))
            {
                throw new BadRequestException("Specified Organization cannot sponsor other organizations.");
            }

            var sponsoringOrgUser = await _organizationUserRepository.GetByIdAsync(model.OrganizationUserId);
            if (sponsoringOrgUser == null || sponsoringOrgUser.Status != OrganizationUserStatusType.Confirmed)
            {
                throw new BadRequestException("Only confirm users can sponsor other organizations.");
            }

            var existingOrgSponsorship = await _organizationSponsorshipRepository.GetBySponsoringOrganizationUserIdAsync(sponsoringOrgUser.Id);
            if (existingOrgSponsorship != null)
            {
                throw new BadRequestException("Can only sponsor one organization per Organization User");
            }

            // TODO: send sponsorship email

            throw new NotImplementedException();
        }

        [HttpPost("sponsored/redeem/families-for-enterprise")]
        public async Task<IActionResult> RedeemSponsorship([FromQuery] string sponsorshipInfo, [FromBody] OrganizationSponsorshipRedeemRequestModel model)
        {
            // TODO: parse out sponsorshipInfo

            if (!await _currentContext.OrganizationOwner(model.SponsoredOrganizationId))
            {
                throw new BadRequestException("Can only redeem sponsorship for and organization you own");
            }

            var existingOrgSponsorship = await _organizationSponsorshipRepository.GetBySponsoredOrganizationIdAsync(model.SponsoredOrganizationId);
            if (existingOrgSponsorship != null)
            {
                throw new BadRequestException("Cannot redeem a sponsorship offer for and organization that is already sponsored. Revoke existing sponsorship first.");
            }

            var organizationToSponsor = await _organizationRepository.GetByIdAsync(model.SponsoredOrganizationId);
            // TODO: only current families plan?
            if (organizationToSponsor == null || !PlanTypeHelper.HasFamiliesPlan(organizationToSponsor))
            {
                throw new BadRequestException("Can only redeem sponsorship offer on families organizations");
            }

            // TODO: check user is owner of proposed org, it isn't currently sponsored, and set up sponsorship
            throw new NotImplementedException();
        }

        [HttpDelete("{sponsoringOrgId}/{sponsoringOrgUserId}")]
        [HttpPost("{sponsoringOrgId}/{sponsoringOrgUserId}/delete")]
        public async Task<IActionResult> RevokeSponsorship(string sponsoringOrgId, string sponsoringOrgUserId)
        {
            var sponsoringOrgIdGuid = new Guid(sponsoringOrgId);
            var sponsoringOrgUserIdGuid = new Guid(sponsoringOrgUserId);

            var orgUser = await _organizationUserRepository.GetByIdAsync(sponsoringOrgUserIdGuid);
            if (_currentContext.UserId != orgUser?.UserId)
            {
                throw new BadRequestException("Can only revoke a sponsorship you own.");
            }

            var existingOrgSponsorship = await _organizationSponsorshipRepository.GetBySponsoringOrganizationUserIdAsync(sponsoringOrgUserIdGuid);
            if (existingOrgSponsorship == null)
            {
                throw new BadRequestException("You are not currently sponsoring and organization.");
            }

            // TODO: remove sponsorship
            throw new NotImplementedException();
        }

        [HttpDelete("sponsored/{sponsoredOrgId}")]
        [HttpPost("sponsored/{sponsoredOrgId}/remove")]
        public async Task<IActionResult> RemoveSponsorship(string sponsoredOrgId)
        {
            var sponsoredOrgIdGuid = new Guid(sponsoredOrgId);

            if (!await _currentContext.OrganizationOwner(sponsoredOrgIdGuid))
            {
                throw new BadRequestException("Only the owner of an organization can remove sponsorship.");
            }

            var existingOrgSponsorship = await _organizationSponsorshipRepository.GetBySponsoringOrganizationUserIdAsync(sponsoredOrgIdGuid);
            if (existingOrgSponsorship == null)
            {
                throw new BadRequestException("The requested organization is not currently being sponsored");
            }

            // TODO: remove sponsorship
            throw new NotImplementedException();
        }
    }
}
