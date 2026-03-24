using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Constants;
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
using Stripe;

namespace Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Cloud;

public class SetUpSponsorshipCommand : ISetUpSponsorshipCommand
{
    private readonly IOrganizationSponsorshipRepository _organizationSponsorshipRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IStripePaymentService _paymentService;
    private readonly IFeatureService _featureService;
    private readonly IPricingClient _pricingClient;
    private readonly IUpdateOrganizationSubscriptionCommand _updateOrganizationSubscriptionCommand;
    private readonly IStripeAdapter _stripeAdapter;
    private readonly ILogger<SetUpSponsorshipCommand> _logger;

    public SetUpSponsorshipCommand(
        IOrganizationSponsorshipRepository organizationSponsorshipRepository,
        IOrganizationRepository organizationRepository,
        IStripePaymentService paymentService,
        IFeatureService featureService,
        IPricingClient pricingClient,
        IUpdateOrganizationSubscriptionCommand updateOrganizationSubscriptionCommand,
        IStripeAdapter stripeAdapter,
        ILogger<SetUpSponsorshipCommand> logger)
    {
        _organizationSponsorshipRepository = organizationSponsorshipRepository;
        _organizationRepository = organizationRepository;
        _paymentService = paymentService;
        _featureService = featureService;
        _pricingClient = pricingClient;
        _updateOrganizationSubscriptionCommand = updateOrganizationSubscriptionCommand;
        _stripeAdapter = stripeAdapter;
        _logger = logger;
    }

    public async Task SetUpSponsorshipAsync(OrganizationSponsorship sponsorship,
        Organization sponsoredOrganization)
    {
        if (sponsorship == null)
        {
            throw new BadRequestException("No unredeemed sponsorship offer exists for you.");
        }

        var existingOrgSponsorship = await _organizationSponsorshipRepository
            .GetBySponsoredOrganizationIdAsync(sponsoredOrganization.Id);
        if (existingOrgSponsorship != null)
        {
            throw new BadRequestException("Cannot redeem a sponsorship offer for an organization that is already sponsored. Revoke existing sponsorship first.");
        }

        if (sponsorship.PlanSponsorshipType == null)
        {
            throw new BadRequestException("Cannot set up sponsorship without a known sponsorship type.");
        }

        // Do not allow self-hosted sponsorships that haven't been synced for > 0.5 year
        if (sponsorship.LastSyncDate != null && DateTime.UtcNow.Subtract(sponsorship.LastSyncDate.Value).TotalDays > 182.5)
        {
            await _organizationSponsorshipRepository.DeleteAsync(sponsorship);
            throw new BadRequestException("This sponsorship offer is more than 6 months old and has expired.");
        }

        // Check org to sponsor's product type
        var sponsoredPlan = SponsoredPlans.Get(sponsorship.PlanSponsorshipType.Value);
        var requiredSponsoredProductType = sponsoredPlan.SponsoredProductTierType;
        var sponsoredOrganizationProductTier = sponsoredOrganization.PlanType.GetProductTier();

        if (sponsoredOrganizationProductTier != requiredSponsoredProductType)
        {
            throw new BadRequestException("Can only redeem sponsorship offer on families organizations.");
        }

        // Release any active subscription schedule before updating the subscription.
        // A schedule may exist if a deferred price migration is pending for this subscription.
        if (_featureService.IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal)
            && !string.IsNullOrEmpty(sponsoredOrganization.GatewaySubscriptionId)
            && !string.IsNullOrEmpty(sponsoredOrganization.GatewayCustomerId))
        {
            try
            {
                var schedules = await _stripeAdapter.ListSubscriptionSchedulesAsync(
                    new SubscriptionScheduleListOptions { Customer = sponsoredOrganization.GatewayCustomerId });

                var activeSchedule = schedules.Data.FirstOrDefault(s =>
                    s.Status == StripeConstants.SubscriptionScheduleStatus.Active && s.SubscriptionId == sponsoredOrganization.GatewaySubscriptionId);
                if (activeSchedule != null)
                {
                    await _stripeAdapter.ReleaseSubscriptionScheduleAsync(activeSchedule.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to release subscription schedule for subscription {SubscriptionId}. Manual release required.",
                    sponsoredOrganization.GatewaySubscriptionId);
                throw;
            }
        }

        if (_featureService.IsEnabled(FeatureFlagKeys.PM32581_UseUpdateOrganizationSubscriptionCommand))
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
    }
}
