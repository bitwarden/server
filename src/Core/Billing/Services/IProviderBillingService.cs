using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.Billing.Models;
using Bit.Core.Enums;
using Bit.Core.Models.Business;

namespace Bit.Core.Billing.Services;

public interface IProviderBillingService
{
    /// <summary>
    /// Assigns a specified number of <paramref name="seats"/> to a client <paramref name="organization"/> on behalf of
    /// its <paramref name="provider"/>. Seat adjustments for the client organization may autoscale the provider's Stripe
    /// <see cref="Stripe.Subscription"/> depending on the provider's seat minimum for the client <paramref name="organization"/>'s
    /// <see cref="PlanType"/>.
    /// </summary>
    /// <param name="provider">The <see cref="Provider"/> that manages the client <paramref name="organization"/>.</param>
    /// <param name="organization">The client <see cref="Organization"/> whose <see cref="seats"/> you want to update.</param>
    /// <param name="seats">The number of seats to assign to the client organization.</param>
    Task AssignSeatsToClientOrganization(
        Provider provider,
        Organization organization,
        int seats);

    /// <summary>
    /// Create a Stripe <see cref="Stripe.Customer"/> for the specified <paramref name="provider"/> utilizing the provided <paramref name="taxInfo"/>.
    /// </summary>
    /// <param name="provider">The <see cref="Provider"/> to create a Stripe customer for.</param>
    /// <param name="taxInfo">The <see cref="TaxInfo"/> to use for calculating the customer's automatic tax.</param>
    /// <returns></returns>
    Task CreateCustomer(
        Provider provider,
        TaxInfo taxInfo);

    /// <summary>
    /// Create a Stripe <see cref="Stripe.Customer"/> for the provided client <paramref name="organization"/> utilizing
    /// the address and tax information of its <paramref name="provider"/>.
    /// </summary>
    /// <param name="provider">The MSP that owns the client organization.</param>
    /// <param name="organization">The client organization to create a Stripe <see cref="Stripe.Customer"/> for.</param>
    Task CreateCustomerForClientOrganization(
        Provider provider,
        Organization organization);

    /// <summary>
    /// Retrieves the number of seats an MSP has assigned to its client organizations with a specified <paramref name="planType"/>.
    /// </summary>
    /// <param name="providerId">The ID of the MSP to retrieve the assigned seat total for.</param>
    /// <param name="planType">The type of plan to retrieve the assigned seat total for.</param>
    /// <returns>An <see cref="int"/> representing the number of seats the provider has assigned to its client organizations with the specified <paramref name="planType"/>.</returns>
    /// <exception cref="BillingException">Thrown when the provider represented by the <paramref name="providerId"/> is <see langword="null"/>.</exception>
    /// <exception cref="BillingException">Thrown when the provider represented by the <paramref name="providerId"/> has <see cref="Provider.Type"/> <see cref="ProviderType.Reseller"/>.</exception>
    Task<int> GetAssignedSeatTotalForPlanOrThrow(
        Guid providerId,
        PlanType planType);

    /// <summary>
    /// Retrieves a provider's billing subscription data.
    /// </summary>
    /// <param name="providerId">The ID of the provider to retrieve subscription data for.</param>
    /// <returns>A <see cref="ProviderSubscriptionDTO"/> object containing the provider's Stripe <see cref="Stripe.Subscription"/> and their <see cref="ConfiguredProviderPlanDTO"/>s.</returns>
    /// <remarks>This method opts for returning <see langword="null"/> rather than throwing exceptions, making it ideal for surfacing data from API endpoints.</remarks>
    Task<ProviderSubscriptionDTO> GetSubscriptionDTO(
        Guid providerId);

    /// <summary>
    /// Scales the <paramref name="provider"/>'s seats for the specified <paramref name="planType"/> using the provided <paramref name="seatAdjustment"/>.
    /// This operation may autoscale the provider's Stripe <see cref="Stripe.Subscription"/> depending on the <paramref name="provider"/>'s seat minimum for the
    /// specified <paramref name="planType"/>.
    /// </summary>
    /// <param name="provider">The <see cref="Provider"/> to scale seats for.</param>
    /// <param name="planType">The <see cref="PlanType"/> to scale seats for.</param>
    /// <param name="seatAdjustment">The change in the number of seats you'd like to apply to the <paramref name="provider"/>.</param>
    Task ScaleSeats(
        Provider provider,
        PlanType planType,
        int seatAdjustment);

    /// <summary>
    /// Starts a Stripe <see cref="Stripe.Subscription"/> for the given <paramref name="provider"/> given it has an existing Stripe <see cref="Stripe.Customer"/>.
    /// <see cref="Provider"/> subscriptions will always be started with a <see cref="Stripe.SubscriptionItem"/> for both the <see cref="PlanType.TeamsMonthly"/>
    /// and <see cref="PlanType.EnterpriseMonthly"/> plan, and the quantity for each item will be equal the provider's seat minimum for each respective plan.
    /// </summary>
    /// <param name="provider">The provider to create the <see cref="Stripe.Subscription"/> for.</param>
    Task StartSubscription(
        Provider provider);

    /// <summary>
    /// Retrieves a provider's billing payment information.
    /// </summary>
    /// <param name="providerId">The ID of the provider to retrieve payment information for.</param>
    /// <returns>A <see cref="ProviderPaymentInfoDTO"/> object containing the provider's Stripe <see cref="Stripe.PaymentMethod"/> and their <see cref="TaxInfo"/>s.</returns>
    /// <remarks>This method opts for returning <see langword="null"/> rather than throwing exceptions, making it ideal for surfacing data from API endpoints.</remarks>
    Task<ProviderPaymentInfoDTO> GetPaymentInformationAsync(Guid providerId);
}
