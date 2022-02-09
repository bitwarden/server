using System;
using System.Threading.Tasks;
using Bit.Api.Models.Request.Organizations;
using Bit.Api.Models.Response.OrganizationSponsorships.FamiliesForEnterprise.SelfHosted;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Controllers.SelfHosted
{
    [Route("organization/sponsorship/self-hosted")]
    [Authorize("Application")]
    [SelfHosted(SelfHostedOnly = true)]
    public class SelfHostedOrganizationSponsorshipsController : Controller
    {
        private readonly IUserService _userService;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly IOrganizationSponsorshipRepository _organizationSponsorshipRepository;
        private readonly ICreateSponsorshipCommand _offerSponsorshipCommand;
        private readonly IGenerateOfferTokenCommand _generateOfferTokenCommand;
        private readonly ISelfHostedRevokeSponsorshipCommand _revokeSponsorshipCommand;
        private readonly IGenerateCancelTokenCommand _generateCancelTokenCommand;
        private readonly ICurrentContext _currentContext;
        private readonly IGlobalSettings _globalSettings;

        public SelfHostedOrganizationSponsorshipsController(
            ICreateSponsorshipCommand offerSponsorshipCommand,
            IGenerateOfferTokenCommand generateOfferTokenCommand,
            ISelfHostedRevokeSponsorshipCommand revokeSponsorshipCommand,
            IGenerateCancelTokenCommand generateCancelTokenCommand,
            IOrganizationRepository organizationRepository,
            IOrganizationSponsorshipRepository organizationSponsorshipRepository,
            IOrganizationUserRepository organizationUserRepository,
            IUserService userService,
            ICurrentContext currentContext,
            IGlobalSettings globalSettings
        )
        {
            _offerSponsorshipCommand = offerSponsorshipCommand;
            _generateOfferTokenCommand = generateOfferTokenCommand;
            _revokeSponsorshipCommand = revokeSponsorshipCommand;
            _generateCancelTokenCommand = generateCancelTokenCommand;
            _organizationRepository = organizationRepository;
            _organizationSponsorshipRepository = organizationSponsorshipRepository;
            _organizationUserRepository = organizationUserRepository;
            _userService = userService;
            _currentContext = currentContext;
            _globalSettings = globalSettings;
        }

        [HttpPost("{sponsoringOrgId}/families-for-enterprise")]
        public async Task<CreateSponsorshipResponseModel> CreateSponsorship(Guid sponsoringOrgId, [FromBody] OrganizationSponsorshipRequestModel model)
        {
            var sponsorship = await _offerSponsorshipCommand.CreateSponsorshipAsync(
                await _organizationRepository.GetByIdAsync(sponsoringOrgId),
                await _organizationUserRepository.GetByOrganizationAsync(sponsoringOrgId, _currentContext.UserId ?? default),
                model.PlanSponsorshipType, model.SponsoredEmail, model.FriendlyName);

            return new CreateSponsorshipResponseModel(
                await _generateOfferTokenCommand.GenerateToken(
                    _globalSettings.Installation.Key,
                    (await CurrentUser).Email,
                    sponsorship));
        }

        [HttpDelete("{sponsoringOrgId}")]
        [HttpPost("{sponsoringOrgId}/delete")]
        [SelfHosted(NotSelfHostedOnly = true)]
        public async Task<RevokeSponsorshipResponseModel> RevokeSponsorship(Guid sponsoringOrganizationId)
        {
            var orgUser = await _organizationUserRepository.GetByOrganizationAsync(sponsoringOrganizationId, _currentContext.UserId ?? default);

            var existingOrgSponsorship = await _organizationSponsorshipRepository
                .GetBySponsoringOrganizationUserIdAsync(orgUser.Id);

            var cancelToken = await _generateCancelTokenCommand.GenerateToken(_globalSettings.Installation.Key, existingOrgSponsorship);
            await _revokeSponsorshipCommand.RevokeSponsorshipAsync(existingOrgSponsorship);

            return new RevokeSponsorshipResponseModel(cancelToken);
        }

        private Task<User> CurrentUser => _userService.GetUserByIdAsync(_currentContext.UserId.Value);
    }
}
