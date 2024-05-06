using Bit.Core.AdminConsole.Entities;

namespace Bit.Core.Billing.Commands;

public interface IRemovePaymentMethodCommand
{
    /// <summary>
    /// Attempts to remove an Organization's saved payment method. If the Stripe <see cref="Stripe.Customer"/> representing the
    /// <see cref="Organization"/> contains a valid <b>"btCustomerId"</b> key in its <see cref="Stripe.Customer.Metadata"/> property,
    /// this command will attempt to remove the Braintree <see cref="Braintree.PaymentMethod"/>. Otherwise, it will attempt to remove the
    /// Stripe <see cref="Stripe.PaymentMethod"/>.
    /// </summary>
    /// <param name="organization">The organization to remove the saved payment method for.</param>
    Task RemovePaymentMethod(Organization organization);
}
