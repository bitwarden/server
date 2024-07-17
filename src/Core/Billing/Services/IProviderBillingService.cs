using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.Billing.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Models;
using Bit.Core.Models.Business;
using Stripe;

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
    /// <returns>A Stripe <see cref="Stripe.Customer"/> representing the <paramref name="provider"/>.</returns>
    Task<Customer> CreateCustomerForSetup(
        Provider provider,
        TaxInfo taxInfo);

    /// <summary>
    /// Create a Stripe <see cref="Stripe.Customer"/> for the provided client <paramref name="organization"/> utilizing
    /// the address and tax information of its <paramref name="provider"/>.
    /// </summary>
    /// <param name="provider">The MSP that owns the client organization.</param>
    /// <param name="organization">The client organization to create a Stripe <see cref="Stripe.Customer"/> for.</param>
    /// <returns>A Stripe <see cref="Stripe.Customer"/> representing the client organization.</returns>
    Task<Customer> CreateCustomerForClientOrganization(
        Provider provider,
        Organization organization);

    /// <summary>
    /// Generate a provider's client invoice report in CSV format for the specified <paramref name="invoiceId"/>. Utilizes the <see cref="ProviderInvoiceItem"/>
    /// records saved for the <paramref name="invoiceId"/> as part of our webhook processing for the <b>"invoice.created"</b> and <b>"invoice.finalized"</b> Stripe events.
    /// </summary>
    /// <param name="invoiceId">The ID of the Stripe <see cref="Stripe.Invoice"/> to generate the report for.</param>
    /// <returns>The provider's client invoice report as a byte array.</returns>
    Task<byte[]> GenerateClientInvoiceReport(
        string invoiceId);

    /// <summary>
    /// Retrieves the <paramref name="provider"/>'s consolidated billing subscription, which includes their Stripe subscription and configured provider plans.
    /// </summary>
    /// <param name="provider">The provider to retrieve the consolidated billing subscription for.</param>
    /// <returns>A <see cref="ConsolidatedBillingSubscriptionDTO"/> containing the provider's Stripe <see cref="Stripe.Subscription"/> and a list of <see cref="ConfiguredProviderPlanDTO"/>s representing their configured plans.</returns>
    /// <remarks>This method opts for returning <see langword="null"/> rather than throwing exceptions, making it ideal for surfacing data from API endpoints.</remarks>
    Task<ConsolidatedBillingSubscriptionDTO> GetConsolidatedBillingSubscription(
        Provider provider);

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
    /// <returns>The Stripe <see cref="Stripe.Subscription"/> for the provider.</returns>
    Task<Subscription> StartSubscriptionForSetup(
        Provider provider);
}
