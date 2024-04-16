using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.Billing.Entities;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Queries;
using Bit.Core.Billing.Repositories;
using Bit.Core.Enums;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.Extensions.Logging;
using static Bit.Core.Billing.Utilities;

namespace Bit.Core.Billing.Commands.Implementations;

public class ScaleSeatsCommand(
    ILogger<ScaleSeatsCommand> logger,
    IPaymentService paymentService,
    IProviderBillingQueries providerBillingQueries,
    IProviderPlanRepository providerPlanRepository) : IScaleSeatsCommand
{
    public async Task ScalePasswordManagerSeats(Provider provider, PlanType planType, int seatAdjustment)
    {
        ArgumentNullException.ThrowIfNull(provider);

        if (provider.Type != ProviderType.Msp)
        {
            logger.LogError("Non-MSP provider ({ProviderID}) cannot scale their Password Manager seats", provider.Id);

            throw ContactSupport();
        }

        if (!planType.SupportsConsolidatedBilling())
        {
            logger.LogError("Cannot scale provider ({ProviderID}) Password Manager seats for plan type {PlanType} as it does not support consolidated billing", provider.Id, planType.ToString());

            throw ContactSupport();
        }

        var providerPlans = await providerPlanRepository.GetByProviderId(provider.Id);

        var providerPlan = providerPlans.FirstOrDefault(providerPlan => providerPlan.PlanType == planType);

        if (providerPlan == null || !providerPlan.IsConfigured())
        {
            logger.LogError("Cannot scale provider ({ProviderID}) Password Manager seats for plan type {PlanType} when their matching provider plan is not configured", provider.Id, planType);

            throw ContactSupport();
        }

        var seatMinimum = providerPlan.SeatMinimum.GetValueOrDefault(0);

        var currentlyAssignedSeatTotal =
            await providerBillingQueries.GetAssignedSeatTotalForPlanOrThrow(provider.Id, planType);

        var newlyAssignedSeatTotal = currentlyAssignedSeatTotal + seatAdjustment;

        var update = CurryUpdateFunction(
            provider,
            providerPlan,
            newlyAssignedSeatTotal);

        /*
         * Below the limit => Below the limit:
         * No subscription update required. We can safely update the organization's seats.
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

    private Func<int, int, Task> CurryUpdateFunction(
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
