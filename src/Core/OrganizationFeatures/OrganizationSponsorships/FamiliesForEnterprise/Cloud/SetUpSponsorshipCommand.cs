using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Models;
using Bit.Core.Billing.Organizations.Commands;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;

namespace Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Cloud;

public class SetUpSponsorshipCommand : ISetUpSponsorshipCommand
{
    private readonly IOrganizationSponsorshipRepository _organizationSponsorshipRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IStripePaymentService _paymentService;
    private readonly IFeatureService _featureService;
    private readonly IPricingClient _pricingClient;
    private readonly IUpdateOrganizationSubscriptionCommand _updateOrganizationSubscriptionCommand;
    private readonly IPriceIncreaseScheduler _priceIncreaseScheduler;
    private readonly ILogger<SetUpSponsorshipCommand> _logger;

    public SetUpSponsorshipCommand(
        IOrganizationSponsorshipRepository organizationSponsorshipRepository,
        IOrganizationRepository organizationRepository,
        IStripePaymentService paymentService,
        IFeatureService featureService,
        IPricingClient pricingClient,
        IUpdateOrganizationSubscriptionCommand updateOrganizationSubscriptionCommand,
        IPriceIncreaseScheduler priceIncreaseScheduler,
        ILogger<SetUpSponsorshipCommand> logger)
    {
        _organizationSponsorshipRepository = organizationSponsorshipRepository;
        _organizationRepository = organizationRepository;
        _paymentService = paymentService;
        _featureService = featureService;
        _pricingClient = pricingClient;
        _updateOrganizationSubscriptionCommand = updateOrganizationSubscriptionCommand;
        _priceIncreaseScheduler = priceIncreaseScheduler;
        _logger = logger;
    }

    public async Task SetUpSponsorshipAsync(OrganizationSponsorship sponsorship,
        Organization sponsoredOrganization)
    {
        if (sponsorship == null)
        {
            _logger.LogWarning("SetUpSponsorship failed: sponsorship is null");
            throw new BadRequestException("No unredeemed sponsorship offer exists for you.");
        }

        _logger.LogInformation(
            "SetUpSponsorship started: SponsorshipId={SponsorshipId}, SponsoredOrgId={SponsoredOrgId}, SponsoringOrgId={SponsoringOrgId}, PlanSponsorshipType={PlanSponsorshipType}, SponsoredOrgPlanType={SponsoredOrgPlanType}",
            sponsorship.Id,
            sponsoredOrganization.Id,
            sponsorship.SponsoringOrganizationId,
            sponsorship.PlanSponsorshipType,
            sponsoredOrganization.PlanType);

        var existingOrgSponsorship = await _organizationSponsorshipRepository
            .GetBySponsoredOrganizationIdAsync(sponsoredOrganization.Id);
        if (existingOrgSponsorship != null)
        {
            _logger.LogWarning(
                "SetUpSponsorship failed: org already sponsored. SponsoredOrgId={SponsoredOrgId}, ExistingSponsorshipId={ExistingSponsorshipId}",
                sponsoredOrganization.Id,
                existingOrgSponsorship.Id);
            throw new BadRequestException("Cannot redeem a sponsorship offer for an organization that is already sponsored. Revoke existing sponsorship first.");
        }

        if (sponsorship.PlanSponsorshipType == null)
        {
            _logger.LogWarning("SetUpSponsorship failed: PlanSponsorshipType is null. SponsorshipId={SponsorshipId}", sponsorship.Id);
            throw new BadRequestException("Cannot set up sponsorship without a known sponsorship type.");
        }

        // Do not allow self-hosted sponsorships that haven't been synced for > 0.5 year
        if (sponsorship.LastSyncDate != null && DateTime.UtcNow.Subtract(sponsorship.LastSyncDate.Value).TotalDays > 182.5)
        {
            _logger.LogWarning(
                "SetUpSponsorship failed: sponsorship expired (>6 months). SponsorshipId={SponsorshipId}, LastSyncDate={LastSyncDate}",
                sponsorship.Id,
                sponsorship.LastSyncDate);
            await _organizationSponsorshipRepository.DeleteAsync(sponsorship);
            throw new BadRequestException("This sponsorship offer is more than 6 months old and has expired.");
        }

        // Check org to sponsor's product type
        var sponsoredPlan = SponsoredPlans.Get(sponsorship.PlanSponsorshipType.Value);
        var requiredSponsoredProductType = sponsoredPlan.SponsoredProductTierType;
        var sponsoredOrganizationProductTier = sponsoredOrganization.PlanType.GetProductTier();

        if (sponsoredOrganizationProductTier != requiredSponsoredProductType)
        {
            _logger.LogWarning(
                "SetUpSponsorship failed: product type mismatch. SponsorshipId={SponsorshipId}, SponsoredOrgProductTier={SponsoredOrgProductTier}, RequiredProductTier={RequiredProductTier}",
                sponsorship.Id,
                sponsoredOrganizationProductTier,
                requiredSponsoredProductType);
            throw new BadRequestException("Can only redeem sponsorship offer on families organizations.");
        }

        if (!string.IsNullOrEmpty(sponsoredOrganization.GatewaySubscriptionId)
            && !string.IsNullOrEmpty(sponsoredOrganization.GatewayCustomerId))
        {
            await _priceIncreaseScheduler.Release(
                sponsoredOrganization.GatewayCustomerId,
                sponsoredOrganization.GatewaySubscriptionId);
        }

        var useNewCommand = _featureService.IsEnabled(FeatureFlagKeys.PM32581_UseUpdateOrganizationSubscriptionCommand);
        _logger.LogInformation(
            "SetUpSponsorship billing path: UseUpdateOrganizationSubscriptionCommand={UseNewCommand}, SponsorshipId={SponsorshipId}",
            useNewCommand,
            sponsorship.Id);

        if (useNewCommand)
        {
            var existingPlan = await _pricingClient.GetPlanOrThrow(sponsoredOrganization.PlanType);
            var changeSet = OrganizationSubscriptionChangeSet.Builder(existingPlan)
                .EstablishSponsorship(sponsoredPlan)
                .Build();

            var result = await _updateOrganizationSubscriptionCommand.Run(sponsoredOrganization, changeSet);
            var updatedSubscription = result.GetValueOrThrow();
            var currentPeriodEnd = updatedSubscription.GetCurrentPeriodEnd();
            sponsoredOrganization.ExpirationDate = currentPeriodEnd;
            sponsorship.ValidUntil = currentPeriodEnd;
        }
        else
        {
            await _paymentService.SponsorOrganizationAsync(sponsoredOrganization, sponsorship);
        }

        await _organizationRepository.UpsertAsync(sponsoredOrganization);
        sponsorship.SponsoredOrganizationId = sponsoredOrganization.Id;
        sponsorship.OfferedToEmail = null;
        await _organizationSponsorshipRepository.UpsertAsync(sponsorship);

        _logger.LogInformation(
            "SetUpSponsorship completed: SponsorshipId={SponsorshipId}, SponsoredOrgId={SponsoredOrgId}",
            sponsorship.Id,
            sponsoredOrganization.Id);
    }
}
