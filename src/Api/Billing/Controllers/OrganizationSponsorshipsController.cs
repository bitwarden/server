// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Api.Models.Request.Organizations;
using Bit.Api.Models.Response;
using Bit.Api.Models.Response.Organizations;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationConnections.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.Api.Request.OrganizationSponsorships;
using Bit.Core.Models.Api.Response.OrganizationSponsorships;
using Bit.Core.Models.Data.Organizations.OrganizationSponsorships;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Bit.SharedWeb.Swagger;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Billing.Controllers;

[Route("organization/sponsorship")]
public class OrganizationSponsorshipsController : Controller
{
    private readonly IOrganizationSponsorshipRepository _organizationSponsorshipRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IValidateRedemptionTokenCommand _validateRedemptionTokenCommand;
    private readonly IValidateBillingSyncKeyCommand _validateBillingSyncKeyCommand;
    private readonly ICreateSponsorshipCommand _createSponsorshipCommand;
    private readonly ISendSponsorshipOfferCommand _sendSponsorshipOfferCommand;
    private readonly ISetUpSponsorshipCommand _setUpSponsorshipCommand;
    private readonly IRevokeSponsorshipCommand _revokeSponsorshipCommand;
    private readonly IRemoveSponsorshipCommand _removeSponsorshipCommand;
    private readonly ICloudSyncSponsorshipsCommand _syncSponsorshipsCommand;
    private readonly ICurrentContext _currentContext;
    private readonly IUserService _userService;
    private readonly IPolicyRepository _policyRepository;
    private readonly IFeatureService _featureService;

    public OrganizationSponsorshipsController(
        IOrganizationSponsorshipRepository organizationSponsorshipRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IValidateRedemptionTokenCommand validateRedemptionTokenCommand,
        IValidateBillingSyncKeyCommand validateBillingSyncKeyCommand,
        ICreateSponsorshipCommand createSponsorshipCommand,
        ISendSponsorshipOfferCommand sendSponsorshipOfferCommand,
        ISetUpSponsorshipCommand setUpSponsorshipCommand,
        IRevokeSponsorshipCommand revokeSponsorshipCommand,
        IRemoveSponsorshipCommand removeSponsorshipCommand,
        ICloudSyncSponsorshipsCommand syncSponsorshipsCommand,
        IUserService userService,
        ICurrentContext currentContext,
        IPolicyRepository policyRepository,
        IFeatureService featureService)
    {
        _organizationSponsorshipRepository = organizationSponsorshipRepository;
        _organizationRepository = organizationRepository;
        _organizationUserRepository = organizationUserRepository;
        _validateRedemptionTokenCommand = validateRedemptionTokenCommand;
        _validateBillingSyncKeyCommand = validateBillingSyncKeyCommand;
        _createSponsorshipCommand = createSponsorshipCommand;
        _sendSponsorshipOfferCommand = sendSponsorshipOfferCommand;
        _setUpSponsorshipCommand = setUpSponsorshipCommand;
        _revokeSponsorshipCommand = revokeSponsorshipCommand;
        _removeSponsorshipCommand = removeSponsorshipCommand;
        _syncSponsorshipsCommand = syncSponsorshipsCommand;
        _userService = userService;
        _currentContext = currentContext;
        _policyRepository = policyRepository;
        _featureService = featureService;
    }

    [Authorize("Application")]
    [HttpPost("{sponsoringOrgId}/families-for-enterprise")]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task CreateSponsorship(Guid sponsoringOrgId, [FromBody] OrganizationSponsorshipCreateRequestModel model)
    {
        var sponsoringOrg = await _organizationRepository.GetByIdAsync(sponsoringOrgId);
        var freeFamiliesSponsorshipPolicy = await _policyRepository.GetByOrganizationIdTypeAsync(sponsoringOrgId,
            PolicyType.FreeFamiliesSponsorshipPolicy);

        if (freeFamiliesSponsorshipPolicy?.Enabled == true)
        {
            throw new BadRequestException("Free Bitwarden Families sponsorship has been disabled by your organization administrator.");
        }

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

        var sponsorship = await _createSponsorshipCommand.CreateSponsorshipAsync(
            sponsoringOrg,
            await _organizationUserRepository.GetByOrganizationAsync(sponsoringOrgId, _currentContext.UserId ?? default),
            model.PlanSponsorshipType,
            model.SponsoredEmail,
            model.FriendlyName,
            model.IsAdminInitiated.GetValueOrDefault(),
            model.Notes);
        if (sponsorship.OfferedToEmail != null)
        {
            await _sendSponsorshipOfferCommand.SendSponsorshipOfferAsync(sponsorship, sponsoringOrg.Name);
        }
    }

    [Authorize("Application")]
    [HttpPost("{sponsoringOrgId}/families-for-enterprise/resend")]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task ResendSponsorshipOffer(Guid sponsoringOrgId, [FromQuery] string sponsoredFriendlyName)
    {
        var freeFamiliesSponsorshipPolicy = await _policyRepository.GetByOrganizationIdTypeAsync(sponsoringOrgId,
            PolicyType.FreeFamiliesSponsorshipPolicy);

        if (freeFamiliesSponsorshipPolicy?.Enabled == true)
        {
            throw new BadRequestException("Free Bitwarden Families sponsorship has been disabled by your organization administrator.");
        }

        var sponsoringOrgUser = await _organizationUserRepository
            .GetByOrganizationAsync(sponsoringOrgId, _currentContext.UserId ?? default);

        var sponsorships = await _organizationSponsorshipRepository.GetManyBySponsoringOrganizationAsync(sponsoringOrgId);
        var filteredSponsorship = sponsorships.FirstOrDefault(s => s.FriendlyName != null && s.FriendlyName.Equals(sponsoredFriendlyName, StringComparison.OrdinalIgnoreCase));
        if (filteredSponsorship != null)
        {
            await _sendSponsorshipOfferCommand.SendSponsorshipOfferAsync(
                await _organizationRepository.GetByIdAsync(sponsoringOrgId),
                sponsoringOrgUser, filteredSponsorship);
        }
    }

    [Authorize("Application")]
    [HttpPost("validate-token")]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task<PreValidateSponsorshipResponseModel> PreValidateSponsorshipToken([FromQuery] string sponsorshipToken)
    {
        var isFreeFamilyPolicyEnabled = false;
        var (isValid, sponsorship) = await _validateRedemptionTokenCommand.ValidateRedemptionTokenAsync(sponsorshipToken, (await CurrentUser).Email);
        if (isValid && sponsorship.SponsoringOrganizationId.HasValue)
        {
            var policy = await _policyRepository.GetByOrganizationIdTypeAsync(sponsorship.SponsoringOrganizationId.Value,
                PolicyType.FreeFamiliesSponsorshipPolicy);
            isFreeFamilyPolicyEnabled = policy?.Enabled ?? false;
        }

        var response = PreValidateSponsorshipResponseModel.From(isValid, isFreeFamilyPolicyEnabled);

        return response;
    }

    [Authorize("Application")]
    [HttpPost("redeem")]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task RedeemSponsorship([FromQuery] string sponsorshipToken, [FromBody] OrganizationSponsorshipRedeemRequestModel model)
    {
        var (valid, sponsorship) = await _validateRedemptionTokenCommand.ValidateRedemptionTokenAsync(sponsorshipToken, (await CurrentUser).Email);

        if (!valid)
        {
            throw new BadRequestException("Failed to parse sponsorship token.");
        }

        if (!await _currentContext.OrganizationOwner(model.SponsoredOrganizationId))
        {
            throw new BadRequestException("Can only redeem sponsorship for an organization you own.");
        }

        var freeFamiliesSponsorshipPolicy = await _policyRepository.GetByOrganizationIdTypeAsync(
            model.SponsoredOrganizationId, PolicyType.FreeFamiliesSponsorshipPolicy);

        if (freeFamiliesSponsorshipPolicy?.Enabled == true)
        {
            throw new BadRequestException("Free Bitwarden Families sponsorship has been disabled by your organization administrator.");
        }

        await _setUpSponsorshipCommand.SetUpSponsorshipAsync(
            sponsorship,
            await _organizationRepository.GetByIdAsync(model.SponsoredOrganizationId));
    }

    [Authorize("Installation")]
    [HttpPost("sync")]
    public async Task<OrganizationSponsorshipSyncResponseModel> Sync([FromBody] OrganizationSponsorshipSyncRequestModel model)
    {
        var sponsoringOrg = await _organizationRepository.GetByIdAsync(model.SponsoringOrganizationCloudId);
        if (!await _validateBillingSyncKeyCommand.ValidateBillingSyncKeyAsync(sponsoringOrg, model.BillingSyncKey))
        {
            throw new BadRequestException("Invalid Billing Sync Key");
        }

        var (syncResponseData, offersToSend) = await _syncSponsorshipsCommand.SyncOrganization(sponsoringOrg, model.ToOrganizationSponsorshipSync().SponsorshipsBatch);
        await _sendSponsorshipOfferCommand.BulkSendSponsorshipOfferAsync(sponsoringOrg.DisplayName(), offersToSend);
        return new OrganizationSponsorshipSyncResponseModel(syncResponseData);
    }

    [Authorize("Application")]
    [HttpDelete("{sponsoringOrganizationId}")]
    [HttpPost("{sponsoringOrganizationId}/delete")]
    [SwaggerExclude("POST")]
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

        await _revokeSponsorshipCommand.RevokeSponsorshipAsync(existingOrgSponsorship);
    }

    [Authorize("Application")]
    [HttpDelete("{sponsoringOrgId}/{sponsoredFriendlyName}/revoke")]
    [SelfHosted(NotSelfHostedOnly = true)]
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
    [HttpDelete("sponsored/{sponsoredOrgId}")]
    [HttpPost("sponsored/{sponsoredOrgId}/remove")]
    [SwaggerExclude("POST")]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task RemoveSponsorship(Guid sponsoredOrgId)
    {

        if (!await _currentContext.OrganizationOwner(sponsoredOrgId))
        {
            throw new BadRequestException("Only the owner of an organization can remove sponsorship.");
        }

        var existingOrgSponsorship = await _organizationSponsorshipRepository
            .GetBySponsoredOrganizationIdAsync(sponsoredOrgId);

        await _removeSponsorshipCommand.RemoveSponsorshipAsync(existingOrgSponsorship);
    }

    [HttpGet("{sponsoringOrgId}/sync-status")]
    public async Task<object> GetSyncStatus(Guid sponsoringOrgId)
    {
        var sponsoringOrg = await _organizationRepository.GetByIdAsync(sponsoringOrgId);

        if (!await _currentContext.OrganizationOwner(sponsoringOrg.Id))
        {
            throw new NotFoundException();
        }

        var lastSyncDate = await _organizationSponsorshipRepository.GetLatestSyncDateBySponsoringOrganizationIdAsync(sponsoringOrg.Id);
        return new OrganizationSponsorshipSyncStatusResponseModel(lastSyncDate);
    }

    [Authorize("Application")]
    [HttpGet("{sponsoringOrgId}/sponsored")]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task<ListResponseModel<OrganizationSponsorshipInvitesResponseModel>> GetSponsoredOrganizations(Guid sponsoringOrgId)
    {
        var sponsoringOrg = await _organizationRepository.GetByIdAsync(sponsoringOrgId);
        if (sponsoringOrg == null)
        {
            throw new NotFoundException();
        }
        var organization = _currentContext.Organizations.First(x => x.Id == sponsoringOrg.Id);
        if (!await _currentContext.OrganizationOwner(sponsoringOrg.Id) && !await _currentContext.OrganizationAdmin(sponsoringOrg.Id) && !organization.Permissions.ManageUsers)
        {
            throw new UnauthorizedAccessException();
        }

        var sponsorships = await _organizationSponsorshipRepository.GetManyBySponsoringOrganizationAsync(sponsoringOrgId);
        return new ListResponseModel<OrganizationSponsorshipInvitesResponseModel>(
            sponsorships
                .Where(s => s.IsAdminInitiated)
                .Select(s => new OrganizationSponsorshipInvitesResponseModel(new OrganizationSponsorshipData(s)))
        );

    }

    private Task<User> CurrentUser => _userService.GetUserByIdAsync(_currentContext.UserId.Value);
}
