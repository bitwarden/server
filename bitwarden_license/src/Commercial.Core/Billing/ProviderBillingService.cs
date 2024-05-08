using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing;
using Bit.Core.Billing.Entities;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Models;
using Bit.Core.Billing.Repositories;
using Bit.Core.Billing.Services;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.Extensions.Logging;
using Stripe;
using static Bit.Core.Billing.Utilities;

namespace Bit.Commercial.Core.Billing;

public class ProviderBillingService(
    ILogger<ProviderBillingService> logger,
    IOrganizationRepository organizationRepository,
    IPaymentService paymentService,
    IProviderBillingService providerBillingService,
    IProviderOrganizationRepository providerOrganizationRepository,
    IProviderPlanRepository providerPlanRepository,
    IProviderRepository providerRepository,
    ISubscriberService subscriberService) : IProviderBillingService
{
    public async Task AssignSeatsToClientOrganization(
        Provider provider,
        Organization organization,
        int seats)
    {
        ArgumentNullException.ThrowIfNull(organization);

        if (seats < 0)
        {
            throw new BillingException(
                "You cannot assign negative seats to a client.",
                "MSP cannot assign negative seats to a client organization");
        }

        if (seats == organization.Seats)
        {
            logger.LogWarning("Client organization ({ID}) already has {Seats} seats assigned to it", organization.Id, organization.Seats);

            return;
        }

        var seatAdjustment = seats - (organization.Seats ?? 0);

        await ScaleSeats(provider, organization.PlanType, seatAdjustment);

        organization.Seats = seats;

        await organizationRepository.ReplaceAsync(organization);
    }

    public async Task<int> GetAssignedSeatTotalForPlanOrThrow(
        Guid providerId,
        PlanType planType)
    {
        var provider = await providerRepository.GetByIdAsync(providerId);

        if (provider == null)
        {
            logger.LogError(
                "Could not find provider ({ID}) when retrieving assigned seat total",
                providerId);

            throw ContactSupport();
        }

        if (provider.Type == ProviderType.Reseller)
        {
            logger.LogError("Assigned seats cannot be retrieved for reseller-type provider ({ID})", providerId);

            throw ContactSupport("Consolidated billing does not support reseller-type providers");
        }

        var providerOrganizations = await providerOrganizationRepository.GetManyDetailsByProviderAsync(providerId);

        var plan = StaticStore.GetPlan(planType);

        return providerOrganizations
            .Where(providerOrganization => providerOrganization.Plan == plan.Name && providerOrganization.Status == OrganizationStatusType.Managed)
            .Sum(providerOrganization => providerOrganization.Seats ?? 0);
    }

    public async Task<ProviderSubscriptionDTO> GetSubscriptionDTO(Guid providerId)
    {
        var provider = await providerRepository.GetByIdAsync(providerId);

        if (provider == null)
        {
            logger.LogError(
                "Could not find provider ({ID}) when retrieving subscription data.",
                providerId);

            return null;
        }

        if (provider.Type == ProviderType.Reseller)
        {
            logger.LogError("Subscription data cannot be retrieved for reseller-type provider ({ID})", providerId);

            throw ContactSupport("Consolidated billing does not support reseller-type providers");
        }

        var subscription = await subscriberService.GetSubscription(provider, new SubscriptionGetOptions
        {
            Expand = ["customer"]
        });

        if (subscription == null)
        {
            return null;
        }

        var providerPlans = await providerPlanRepository.GetByProviderId(providerId);

        var configuredProviderPlans = providerPlans
            .Where(providerPlan => providerPlan.IsConfigured())
            .Select(ConfiguredProviderPlanDTO.From)
            .ToList();

        return new ProviderSubscriptionDTO(
            configuredProviderPlans,
            subscription);
    }

    public async Task ScaleSeats(
        Provider provider,
        PlanType planType,
        int seatAdjustment)
    {
        ArgumentNullException.ThrowIfNull(provider);

        if (provider.Type != ProviderType.Msp)
        {
            logger.LogError("Non-MSP provider ({ProviderID}) cannot scale their seats", provider.Id);

            throw ContactSupport();
        }

        if (!planType.SupportsConsolidatedBilling())
        {
            logger.LogError("Cannot scale provider ({ProviderID}) seats for plan type {PlanType} as it does not support consolidated billing", provider.Id, planType.ToString());

            throw ContactSupport();
        }

        var providerPlans = await providerPlanRepository.GetByProviderId(provider.Id);

        var providerPlan = providerPlans.FirstOrDefault(providerPlan => providerPlan.PlanType == planType);

        if (providerPlan == null || !providerPlan.IsConfigured())
        {
            logger.LogError("Cannot scale provider ({ProviderID}) seats for plan type {PlanType} when their matching provider plan is not configured", provider.Id, planType);

            throw ContactSupport();
        }

        var seatMinimum = providerPlan.SeatMinimum.GetValueOrDefault(0);

        var currentlyAssignedSeatTotal =
            await providerBillingService.GetAssignedSeatTotalForPlanOrThrow(provider.Id, planType);

        var newlyAssignedSeatTotal = currentlyAssignedSeatTotal + seatAdjustment;

        var update = CurrySeatScalingUpdate(
            provider,
            providerPlan,
            newlyAssignedSeatTotal);

        /*
         * Below the limit => Below the limit:
         * No subscription update required. We can safely update the provider's allocated seats.
         */
        if (currentlyAssignedSeatTotal <= seatMinimum &&
            newlyAssignedSeatTotal <= seatMinimum)
        {
            providerPlan.AllocatedSeats = newlyAssignedSeatTotal;

            await providerPlanRepository.ReplaceAsync(providerPlan);
        }
        /*
         * Below the limit => Above the limit:
         * We have to scale the subscription up from the seat minimum to the newly assigned seat total.
         */
        else if (currentlyAssignedSeatTotal <= seatMinimum &&
                 newlyAssignedSeatTotal > seatMinimum)
        {
            await update(
                seatMinimum,
                newlyAssignedSeatTotal);
        }
        /*
         * Above the limit => Above the limit:
         * We have to scale the subscription from the currently assigned seat total to the newly assigned seat total.
         */
        else if (currentlyAssignedSeatTotal > seatMinimum &&
                 newlyAssignedSeatTotal > seatMinimum)
        {
            await update(
                currentlyAssignedSeatTotal,
                newlyAssignedSeatTotal);
        }
        /*
         * Above the limit => Below the limit:
         * We have to scale the subscription down from the currently assigned seat total to the seat minimum.
         */
        else if (currentlyAssignedSeatTotal > seatMinimum &&
                 newlyAssignedSeatTotal <= seatMinimum)
        {
            await update(
                currentlyAssignedSeatTotal,
                seatMinimum);
        }
    }

    private Func<int, int, Task> CurrySeatScalingUpdate(
        Provider provider,
        ProviderPlan providerPlan,
        int newlyAssignedSeats) => async (currentlySubscribedSeats, newlySubscribedSeats) =>
    {
        var plan = StaticStore.GetPlan(providerPlan.PlanType);

        await paymentService.AdjustSeats(
            provider,
            plan,
            currentlySubscribedSeats,
            newlySubscribedSeats);

        var newlyPurchasedSeats = newlySubscribedSeats > providerPlan.SeatMinimum
            ? newlySubscribedSeats - providerPlan.SeatMinimum
            : 0;

        providerPlan.PurchasedSeats = newlyPurchasedSeats;
        providerPlan.AllocatedSeats = newlyAssignedSeats;

        await providerPlanRepository.ReplaceAsync(providerPlan);
    };
}
