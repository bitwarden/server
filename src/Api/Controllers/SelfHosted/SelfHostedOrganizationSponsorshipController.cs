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
    public class SelfHostedOrganizationSponsorshipConstroller : Controller
    {
        private readonly IUserService _userService;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly ICreateSponsorshipCommand _offerSponsorshipCommand;
        private readonly IGenerateOfferTokenCommand _generateOfferTokenCommand;
        private readonly ICurrentContext _currentContext;
        private readonly IGlobalSettings _globalSettings;

        public SelfHostedOrganizationSponsorshipConstroller(
            ICreateSponsorshipCommand offerSponsorshipCommand,
            IGenerateOfferTokenCommand generateOfferTokenCommand,
            IOrganizationRepository organizationRepository,
            IOrganizationUserRepository organizationUserRepository,
            IUserService userService,
            ICurrentContext currentContext,
            IGlobalSettings globalSettings
        )
        {
            _offerSponsorshipCommand = offerSponsorshipCommand;
            _generateOfferTokenCommand = generateOfferTokenCommand;
            _organizationRepository = organizationRepository;
            _organizationUserRepository = organizationUserRepository;
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
                _generateOfferTokenCommand.GenerateToken(
                    _globalSettings.Installation.Key,
                    (await CurrentUser).Email,
                    sponsorship));
        }

        private Task<User> CurrentUser => _userService.GetUserByIdAsync(_currentContext.UserId.Value);
    }
}
