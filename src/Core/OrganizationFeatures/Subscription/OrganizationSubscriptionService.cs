using System;
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
        }
    }
}
