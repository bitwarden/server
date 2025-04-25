﻿using Bit.Api.Models.Request.Organizations;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Controllers.SelfHosted;

[Route("organization/sponsorship/self-hosted")]
[Authorize("Application")]
[SelfHosted(SelfHostedOnly = true)]
public class SelfHostedOrganizationSponsorshipsController : Controller
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IOrganizationSponsorshipRepository _organizationSponsorshipRepository;
    private readonly ICreateSponsorshipCommand _offerSponsorshipCommand;
    private readonly IRevokeSponsorshipCommand _revokeSponsorshipCommand;
    private readonly ICurrentContext _currentContext;
    private readonly IFeatureService _featureService;

    public SelfHostedOrganizationSponsorshipsController(
        ICreateSponsorshipCommand offerSponsorshipCommand,
        IRevokeSponsorshipCommand revokeSponsorshipCommand,
        IOrganizationRepository organizationRepository,
        IOrganizationSponsorshipRepository organizationSponsorshipRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICurrentContext currentContext,
        IFeatureService featureService
    )
    {
        _offerSponsorshipCommand = offerSponsorshipCommand;
        _revokeSponsorshipCommand = revokeSponsorshipCommand;
        _organizationRepository = organizationRepository;
        _organizationSponsorshipRepository = organizationSponsorshipRepository;
        _organizationUserRepository = organizationUserRepository;
        _currentContext = currentContext;
        _featureService = featureService;
    }

    [HttpPost("{sponsoringOrgId}/families-for-enterprise")]
    public async Task CreateSponsorship(Guid sponsoringOrgId, [FromBody] OrganizationSponsorshipCreateRequestModel model)
    {
        if (!_featureService.IsEnabled(Bit.Core.FeatureFlagKeys.PM17772_AdminInitiatedSponsorships))
        {
            if (model.IsAdminInitiated.GetValueOrDefault())
            {
                throw new BadRequestException();
            }

            if (!string.IsNullOrWhiteSpace(model.Notes))
            {
                model.Notes = null;
            }
        }

        await _offerSponsorshipCommand.CreateSponsorshipAsync(
            await _organizationRepository.GetByIdAsync(sponsoringOrgId),
            await _organizationUserRepository.GetByOrganizationAsync(sponsoringOrgId, _currentContext.UserId ?? default),
            model.PlanSponsorshipType,
            model.SponsoredEmail,
            model.FriendlyName,
            model.IsAdminInitiated.GetValueOrDefault(),
            model.Notes);
    }

    [HttpDelete("{sponsoringOrgId}")]
    [HttpPost("{sponsoringOrgId}/delete")]
    public async Task RevokeSponsorship(Guid sponsoringOrgId)
    {
        var orgUser = await _organizationUserRepository.GetByOrganizationAsync(sponsoringOrgId, _currentContext.UserId ?? default);

        if (orgUser == null)
        {
            throw new BadRequestException("Unknown Organization User");
        }

        var existingOrgSponsorship = await _organizationSponsorshipRepository
            .GetBySponsoringOrganizationUserIdAsync(orgUser.Id);

        await _revokeSponsorshipCommand.RevokeSponsorshipAsync(existingOrgSponsorship);
    }
}
