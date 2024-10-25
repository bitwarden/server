using Bit.Core.Billing.Models.Sales;
using Bit.Core.Entities;

namespace Bit.Core.Billing.Services;

public interface IPremiumUserBillingService
{
    /// <summary>
    /// <para>Establishes the Stripe entities necessary for a Bitwarden <see cref="User"/> using the provided <paramref name="sale"/>.</para>
    /// <para>
    /// The method first checks to see if the
    /// provided <see cref="PremiumUserSale.User"/> already has a Stripe <see cref="Stripe.Customer"/> using the <see cref="User.GatewayCustomerId"/>.
    /// If it doesn't, the method creates one using the <paramref name="sale"/>'s <see cref="PremiumUserSale.CustomerSetup"/>. The method then creates a Stripe <see cref="Stripe.Subscription"/>
    /// for the created or existing customer while appending the provided <paramref name="sale"/>'s <see cref="PremiumUserSale.Storage"/>.
    /// </para>
    /// </summary>
    /// <param name="sale">The data required to establish the Stripe entities responsible for billing the premium user.</param>
    /// <example>
    /// <code>
    /// var sale = PremiumUserSale.From(
    ///     user,
    ///     paymentMethodType,
    ///     paymentMethodToken,
    ///     taxInfo,
    ///     storage);
    /// await premiumUserBillingService.Finalize(sale);
    /// </code>
    /// </example>
    Task Finalize(PremiumUserSale sale);
}
