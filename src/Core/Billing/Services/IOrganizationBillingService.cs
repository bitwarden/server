using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Models;
using Bit.Core.Billing.Models.Sales;

namespace Bit.Core.Billing.Services;

#nullable enable

public interface IOrganizationBillingService
{
    /// <summary>
    /// <para>Establishes the Stripe entities necessary for a Bitwarden <see cref="Organization"/> using the provided <paramref name="sale"/>.</para>
    /// <para>
    /// The method first checks to see if the
    /// provided <see cref="OrganizationSale.Organization"/> already has a Stripe <see cref="Stripe.Customer"/> using the <see cref="Organization.GatewayCustomerId"/>.
    /// If it doesn't, the method creates one using the <paramref name="sale"/>'s <see cref="OrganizationSale.CustomerSetup"/>. The method then creates a Stripe <see cref="Stripe.Subscription"/>
    /// for the created or existing customer using the provided <see cref="OrganizationSale.SubscriptionSetup"/>.
    /// </para>
    /// </summary>
    /// <param name="sale">The data required to establish the Stripe entities responsible for billing the organization.</param>
    /// <example>
    /// <code>
    /// var sale = OrganizationSale.From(organization, organizationSignup);
    /// await organizationBillingService.Finalize(sale);
    /// </code>
    /// </example>
    Task Finalize(OrganizationSale sale);

    /// <summary>
    /// Retrieve metadata about the organization represented bsy the provided <paramref name="organizationId"/>.
    /// </summary>
    /// <param name="organizationId">The ID of the organization to retrieve metadata for.</param>
    /// <returns>An <see cref="OrganizationMetadata"/> record.</returns>
    Task<OrganizationMetadata?> GetMetadata(Guid organizationId);

    /// <summary>
    /// Updates the provided <paramref name="organization"/>'s payment source and tax information.
    /// If the <paramref name="organization"/> does not have a Stripe <see cref="Stripe.Customer"/>, this method will create one using the provided
    /// <paramref name="tokenizedPaymentSource"/> and <paramref name="taxInformation"/>.
    /// </summary>
    /// <param name="organization">The <paramref name="organization"/> to update the payment source and tax information for.</param>
    /// <param name="tokenizedPaymentSource">The tokenized payment source (ex. Credit Card) to attach to the <paramref name="organization"/>.</param>
    /// <param name="taxInformation">The <paramref name="organization"/>'s updated tax information.</param>
    Task UpdatePaymentMethod(
        Organization organization,
        TokenizedPaymentSource tokenizedPaymentSource,
        TaxInformation taxInformation
    );
}
