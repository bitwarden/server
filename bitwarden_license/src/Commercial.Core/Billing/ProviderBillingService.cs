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
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(organization);

        if (provider.Type == ProviderType.Reseller)
        {
            logger.LogError("Reseller-type provider ({ID}) cannot assign seats to client organizations", provider.Id);

            throw ContactSupport("Consolidated billing does not support reseller-type providers");
        }

        if (seats < 0)
        {
            throw new BillingException(
                "You cannot assign negative seats to a client.",
                "MSP cannot assign negative seats to a client organization");
        }

        if (seats == organization.Seats)
        {
            logger.LogWarning("Client organization ({ID}) already has {Seats} seats assigned", organization.Id, organization.Seats);

            return;
        }

        var providerPlan = await GetProviderPlanForClientOrganizationAsync(provider, organization);

        var providerSeatMinimum = providerPlan.SeatMinimum.GetValueOrDefault(0);

        // How many seats the provider has assigned to all their client organizations that have the specified plan type.
        var providerCurrentlyAssignedSeatTotal = await providerBillingService.GetAssignedSeatTotalForPlanOrThrow(provider.Id, providerPlan.PlanType);

        // How many seats are being added to or subtracted from this client organization.
        var seatDifference = seats - (organization.Seats ?? 0);

        // How many seats the provider will have assigned to all of their client organizations after the update.
        var providerNewlyAssignedSeatTotal = providerCurrentlyAssignedSeatTotal + seatDifference;

        var update = CurrySeatAssignmentUpdate(
            provider,
            providerPlan,
            organization,
            seats,
            providerNewlyAssignedSeatTotal);

        /*
         * Below the limit => Below the limit:
         * No subscription update required. We can safely update the organization's seats.
         */
        if (providerCurrentlyAssignedSeatTotal <= providerSeatMinimum &&
            providerNewlyAssignedSeatTotal <= providerSeatMinimum)
        {
            organization.Seats = seats;

            await organizationRepository.ReplaceAsync(organization);

            providerPlan.AllocatedSeats = providerNewlyAssignedSeatTotal;

            await providerPlanRepository.ReplaceAsync(providerPlan);
        }
        /*
         * Below the limit => Above the limit:
         * We have to scale the subscription up from the seat minimum to the newly assigned seat total.
         */
        else if (providerCurrentlyAssignedSeatTotal <= providerSeatMinimum &&
                 providerNewlyAssignedSeatTotal > providerSeatMinimum)
        {
            await update(
                providerSeatMinimum,
                providerNewlyAssignedSeatTotal);
        }
        /*
         * Above the limit => Above the limit:
         * We have to scale the subscription from the currently assigned seat total to the newly assigned seat total.
         */
        else if (providerCurrentlyAssignedSeatTotal > providerSeatMinimum &&
                 providerNewlyAssignedSeatTotal > providerSeatMinimum)
        {
            await update(
                providerCurrentlyAssignedSeatTotal,
                providerNewlyAssignedSeatTotal);
        }
        /*
         * Above the limit => Below the limit:
         * We have to scale the subscription down from the currently assigned seat total to the seat minimum.
         */
        else if (providerCurrentlyAssignedSeatTotal > providerSeatMinimum &&
                 providerNewlyAssignedSeatTotal <= providerSeatMinimum)
        {
            await update(
                providerCurrentlyAssignedSeatTotal,
                providerSeatMinimum);
        }
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

    #region Utilities
    private Func<int, int, Task> CurrySeatAssignmentUpdate(
        Provider provider,
        ProviderPlan providerPlan,
        Organization organization,
        int organizationNewlyAssignedSeats,
        int providerNewlyAssignedSeats) => async (providerCurrentlySubscribedSeats, providerNewlySubscribedSeats) =>
    {
        var plan = StaticStore.GetPlan(providerPlan.PlanType);

        await paymentService.AdjustSeats(
            provider,
            plan,
            providerCurrentlySubscribedSeats,
            providerNewlySubscribedSeats);

        organization.Seats = organizationNewlyAssignedSeats;

        await organizationRepository.ReplaceAsync(organization);

        var providerNewlyPurchasedSeats = providerNewlySubscribedSeats > providerPlan.SeatMinimum
            ? providerNewlySubscribedSeats - providerPlan.SeatMinimum
            : 0;

        providerPlan.PurchasedSeats = providerNewlyPurchasedSeats;
        providerPlan.AllocatedSeats = providerNewlyAssignedSeats;

        await providerPlanRepository.ReplaceAsync(providerPlan);
    };

    // ReSharper disable once SuggestBaseTypeForParameter
    private async Task<ProviderPlan> GetProviderPlanForClientOrganizationAsync(Provider provider, Organization organization)
    {
        if (!organization.PlanType.SupportsConsolidatedBilling())
        {
            logger.LogError("Client organization ({ID}) has a plan type that does not support consolidated billing: {PlanType}", organization.Id, organization.PlanType);

            throw ContactSupport();
        }

        var providerPlans = await providerPlanRepository.GetByProviderId(provider.Id);

        var providerPlan = providerPlans.FirstOrDefault(providerPlan => providerPlan.PlanType == organization.PlanType);

        if (providerPlan != null && providerPlan.IsConfigured())
        {
            return providerPlan;
        }

        logger.LogError("Client organization ({ClientOrganizationID}) has a provider ({ProviderID}) with a matching plan type that is not configured", organization.Id, provider.Id);

        throw ContactSupport();
    }
    #endregion
}
