using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
using Bit.Core.Models.Table;
using Bit.Core.OrganizationFeatures.Mail;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace Bit.Core.OrganizationFeatures.Subscription
{
    public class OrganizationSubscriptionService : IOrganizationSubscriptionService
    {
        readonly IOrganizationSubscriptionAccessPolicies _organizationSubscriptionAccessPolicies;
        readonly IOrganizationService _organizationService;
        readonly IOrganizationRepository _organizationRepository;
        readonly IOrganizationUserRepository _organizationUserRepository;
        readonly IPaymentService _paymentService;
        readonly IOrganizationUserMailer _organizationUserMailer;
        readonly IReferenceEventService _referenceEventService;
        readonly ILogger<OrganizationSubscriptionService> _logger;

        public OrganizationSubscriptionService(
            IOrganizationSubscriptionAccessPolicies organizationSubscriptionAccessPolicies,
            IOrganizationService organizationService,
            IOrganizationRepository organizationRepository,
            IOrganizationUserRepository organizationUserRepository,
            IPaymentService paymentService,
            IOrganizationUserMailer organizationUserMailer,
            IReferenceEventService referenceEventService,
            ILogger<OrganizationSubscriptionService> logger
        )
        {
            _organizationSubscriptionAccessPolicies = organizationSubscriptionAccessPolicies;
            _organizationService = organizationService;
            _organizationRepository = organizationRepository;
            _organizationUserRepository = organizationUserRepository;
            _paymentService = paymentService;
            _organizationUserMailer = organizationUserMailer;
            _referenceEventService = referenceEventService;
            _logger = logger;
        }

        public async Task CancelSubscriptionAsync(Guid organizationId, bool? endOfPeriod = null)
        {
            var organization = await _organizationRepository.GetByIdAsync(organizationId);
            CoreHelpers.HandlePermissionResult(
                _organizationSubscriptionAccessPolicies.CanCancel(organization)
            );

            var cancelAtPeriodEnd = endOfPeriod.GetValueOrDefault(true);
            if (!endOfPeriod.HasValue && organization.ExpirationDate.HasValue &&
                organization.ExpirationDate.Value < DateTime.UtcNow)
            {
                cancelAtPeriodEnd = false;
            }

            await _paymentService.CancelSubscriptionAsync(organization, cancelAtPeriodEnd);
            await _referenceEventService.RaiseEventAsync(
                new ReferenceEvent(ReferenceEventType.CancelSubscription, organization)
                {
                    EndOfPeriod = endOfPeriod,
                });
        }

        public async Task ReinstateSubscriptionAsync(Guid organizationId)
        {
            var organization = await _organizationRepository.GetByIdAsync(organizationId);
            CoreHelpers.HandlePermissionResult(
                _organizationSubscriptionAccessPolicies.CanReinstate(organization)
            );

            await _paymentService.ReinstateSubscriptionAsync(organization);
            await _referenceEventService.RaiseEventAsync(
                new ReferenceEvent(ReferenceEventType.ReinstateSubscription, organization));
        }

        public async Task UpdateSubscription(Guid organizationId, int seatAdjustment, int? maxAutoscaleSeats)
        {
            var organization = await _organizationRepository.GetByIdAsync(organizationId);

            CoreHelpers.HandlePermissionResult(
                _organizationSubscriptionAccessPolicies.CanUpdateSubscription(organization, seatAdjustment, maxAutoscaleSeats)
            );

            if (seatAdjustment != 0)
            {
                await AdjustSeatsAsync(organization, seatAdjustment);
            }
            if (maxAutoscaleSeats != organization.MaxAutoscaleSeats)
            {
                await UpdateAutoscalingAsync(organization, maxAutoscaleSeats);
            }
        }

        public async Task<(bool success, string paymentIntentClientSecret)> UpgradePlanAsync(Guid organizationId,
            OrganizationUpgrade upgrade)
        {
            var organization = await _organizationRepository.GetByIdAsync(organizationId);

            CoreHelpers.HandlePermissionResult(
                await _organizationSubscriptionAccessPolicies.CanUpgradePlanAsync(organization, upgrade)
            );

            var existingPlan = StaticStore.Plans.FirstOrDefault(p => p.Type == organization.PlanType);
            var newPlan = StaticStore.Plans.FirstOrDefault(p => p.Type == upgrade.Plan && !p.Disabled);

            var paymentIntentClientSecret = await _paymentService.UpgradeFreeOrganizationAsync(organization, newPlan,
                upgrade.AdditionalStorageGb, upgrade.AdditionalSeats, upgrade.PremiumAccessAddon, upgrade.TaxInfo);
            var success = string.IsNullOrWhiteSpace(paymentIntentClientSecret);

            organization = upgrade.ApplyToOrganization(organization, success);

            await _organizationService.ReplaceAndUpdateCache(organization);
            if (success)
            {
                await _referenceEventService.RaiseEventAsync(
                    new ReferenceEvent(ReferenceEventType.UpgradePlan, organization)
                    {
                        PlanName = newPlan.Name,
                        PlanType = newPlan.Type,
                        OldPlanName = existingPlan.Name,
                        OldPlanType = existingPlan.Type,
                        Seats = organization.Seats,
                        Storage = organization.MaxStorageGb,
                    });
            }

            return (success, paymentIntentClientSecret);
        }

        public async Task<string> AdjustSeatsAsync(Organization organization, int seatAdjustment, DateTime? prorationDate = null)
        {
            var userCount = await _organizationUserRepository.GetCountByOrganizationIdAsync(organization.Id);
            var plan = StaticStore.Plans.FirstOrDefault(p => p.Type == organization.PlanType);
            var newSeatTotal = organization.Seats.Value + seatAdjustment;
            var additionalSeats = newSeatTotal - plan.BaseSeats;

            CoreHelpers.HandlePermissionResult(
                _organizationSubscriptionAccessPolicies.CanAdjustSeats(organization, seatAdjustment, userCount)
            );

            // TODO: move payment service to infrastructure layer
            var paymentIntentClientSecret = await _paymentService.AdjustSeatsAsync(organization, plan, additionalSeats, prorationDate);
            await _referenceEventService.RaiseEventAsync(
                new ReferenceEvent(ReferenceEventType.AdjustSeats, organization)
                {
                    PlanName = plan.Name,
                    PlanType = plan.Type,
                    Seats = newSeatTotal,
                    PreviousSeats = organization.Seats
                });
            organization.Seats = (short?)newSeatTotal;
            // TODO: move organization service to infrastructure layer
            await _organizationService.ReplaceAndUpdateCache(organization);

            if (organization.Seats.HasValue && organization.MaxAutoscaleSeats.HasValue && organization.Seats == organization.MaxAutoscaleSeats)
            {
                try
                {
                    await _organizationUserMailer.SendOrganizationMaxSeatLimitReachedEmailAsync(organization, organization.MaxAutoscaleSeats.Value);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Error encountered notifying organization owners of seat limit reached.");
                }
            }

            return paymentIntentClientSecret;
        }

        public async Task<string> AdjustStorageAsync(Guid organizationId, short storageAdjustmentGb)
        {
            var organization = await _organizationRepository.GetByIdAsync(organizationId);
            var plan = StaticStore.Plans.FirstOrDefault(p => p.Type == organization.PlanType);

            CoreHelpers.HandlePermissionResult(
                _organizationSubscriptionAccessPolicies.CanAdjustStorage(organization)
            );

            var secret = await BillingHelpers.AdjustStorageAsync(_paymentService, organization, storageAdjustmentGb,
                plan.StripeStoragePlanId);
            await _referenceEventService.RaiseEventAsync(
                new ReferenceEvent(ReferenceEventType.AdjustStorage, organization)
                {
                    PlanName = plan.Name,
                    PlanType = plan.Type,
                    Storage = storageAdjustmentGb,
                });

            await _organizationService.ReplaceAndUpdateCache(organization);
            return secret;
        }

        public async Task AutoAddSeatsAsync(Organization organization, int newSeatsRequired, DateTime? prorationDate = null)
        {
            if (newSeatsRequired < 1 || !organization.Seats.HasValue)
            {
                return;
            }

            CoreHelpers.HandlePermissionResult(
                _organizationSubscriptionAccessPolicies.CanScale(organization, newSeatsRequired)
            );

            var initialSeatCount = organization.Seats.Value;

            try
            {
                // TODO: should fail if customer interaction required (paymentIntentClientSecret != null)
                await AdjustSeatsAsync(organization, newSeatsRequired, prorationDate);
            }
            catch
            {
                var currentSeatCount = (await _organizationRepository.GetByIdAsync(organization.Id)).Seats;

                if (currentSeatCount.HasValue && currentSeatCount.Value != initialSeatCount)
                {
                    await AdjustSeatsAsync(organization, initialSeatCount - currentSeatCount.Value, prorationDate);
                }

                throw;
            }

            await _organizationUserMailer.SendOrganizationAutoscaledEmailAsync(organization, initialSeatCount);
        }

        private async Task UpdateAutoscalingAsync(Organization organization, int? maxAutoscaleSeats)
        {
            CoreHelpers.HandlePermissionResult(
                _organizationSubscriptionAccessPolicies.CanUpdateAutoscaling(organization, maxAutoscaleSeats)
            );

            organization.MaxAutoscaleSeats = maxAutoscaleSeats;

            await _organizationService.ReplaceAndUpdateCache(organization);
        }
    }
}
