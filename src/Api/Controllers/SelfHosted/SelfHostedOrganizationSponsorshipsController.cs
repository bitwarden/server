// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Api.AdminConsole.Authorization.Requirements;
using Bit.Api.Models.Request.Organizations;
using Bit.Api.Models.Response;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Models.Api.Response.OrganizationSponsorships;
using Bit.Core.Models.Data.Organizations.OrganizationSponsorships;
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
    private readonly IAuthorizationService _authorizationService;

    public SelfHostedOrganizationSponsorshipsController(
        ICreateSponsorshipCommand offerSponsorshipCommand,
        IRevokeSponsorshipCommand revokeSponsorshipCommand,
        IOrganizationRepository organizationRepository,
        IOrganizationSponsorshipRepository organizationSponsorshipRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICurrentContext currentContext,
        IFeatureService featureService,
        IAuthorizationService authorizationService
    )
    {
        _offerSponsorshipCommand = offerSponsorshipCommand;
        _revokeSponsorshipCommand = revokeSponsorshipCommand;
        _organizationRepository = organizationRepository;
        _organizationSponsorshipRepository = organizationSponsorshipRepository;
        _organizationUserRepository = organizationUserRepository;
        _currentContext = currentContext;
        _featureService = featureService;
        _authorizationService = authorizationService;
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

    [HttpPost("{sponsoringOrgId}/delete")]
    [Obsolete("This endpoint is deprecated. Use DELETE /{sponsoringOrgId} instead.")]
    public async Task PostRevokeSponsorship(Guid sponsoringOrgId)
    {
        await RevokeSponsorship(sponsoringOrgId);
    }

    [HttpDelete("{sponsoringOrgId}/{sponsoredFriendlyName}/revoke")]
    public async Task AdminInitiatedRevokeSponsorshipAsync(Guid sponsoringOrgId, string sponsoredFriendlyName)
    {
        var sponsorships = await _organizationSponsorshipRepository.GetManyBySponsoringOrganizationAsync(sponsoringOrgId);
        var existingOrgSponsorship = sponsorships.FirstOrDefault(s => s.FriendlyName != null && s.FriendlyName.Equals(sponsoredFriendlyName, StringComparison.OrdinalIgnoreCase));
        if (existingOrgSponsorship == null)
        {
            throw new BadRequestException("The specified sponsored organization could not be found under the given sponsoring organization.");
        }
        await _revokeSponsorshipCommand.RevokeSponsorshipAsync(existingOrgSponsorship);
    }

    [Authorize("Application")]
    [HttpGet("{orgId}/sponsored")]
    public async Task<ListResponseModel<OrganizationSponsorshipInvitesResponseModel>> GetSponsoredOrganizations(Guid orgId)
    {
        var sponsoringOrg = await _organizationRepository.GetByIdAsync(orgId);
        if (sponsoringOrg == null)
        {
            throw new NotFoundException();
        }

        var authorizationResult = await _authorizationService.AuthorizeAsync(User, orgId, new ManageUsersRequirement());
        if (!authorizationResult.Succeeded)
        {
            throw new UnauthorizedAccessException();
        }

        var sponsorships = await _organizationSponsorshipRepository.GetManyBySponsoringOrganizationAsync(orgId);
        return new ListResponseModel<OrganizationSponsorshipInvitesResponseModel>(
            sponsorships
                .Where(s => s.IsAdminInitiated)
                .Select(s => new OrganizationSponsorshipInvitesResponseModel(new OrganizationSponsorshipData(s)))
        );

    }
}
